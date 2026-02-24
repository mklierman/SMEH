using System.Net.Http.Headers;
using System.Text.Json;
using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Downloads Wwise-CLI, installs it into the starter project, and runs Wwise integration; menu option 6.</summary>
public class WwiseCliService
{
    private readonly WwiseCliOptions _options;
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("SMEH", "1.0") } }
    };

    public WwiseCliService(WwiseCliOptions options, CssUnrealEngineOptions cssUnrealEngineOptions, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        var repo = _options.Repository.Trim();
        if (string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]WwiseCli Repository is not configured.[/]");
            return false;
        }

        string releaseUrl = _options.UseLatest
            ? $"https://api.github.com/repos/{repo}/releases/latest"
            : $"https://api.github.com/repos/{repo}/releases/tags/{_options.ReleaseTag}";

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Fetching release info...[/]");
        using var response = await HttpClient.GetAsync(releaseUrl);
        if (!response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to get release: {response.StatusCode}. Check Repository and ReleaseTag.[/]");
            return false;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var assets = root.GetProperty("assets");
        string? downloadUrl = null;
        string? assetName = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains("windows", StringComparison.OrdinalIgnoreCase) && (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                assetName = name;
                break;
            }
        }

        if (downloadUrl == null || assetName == null)
        {
            AnsiConsole.MarkupLine("[red]No Windows executable or zip found in this release.[/]");
            return false;
        }

        var tempDir = Path.Combine(CleanupService.TempRoot, "WwiseCLI");
        Directory.CreateDirectory(tempDir);
        var destPath = Path.Combine(tempDir, assetName);

        AnsiConsole.MarkupLineInterpolated($"[dim]Downloading {Markup.Escape(assetName)}...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Wwise-CLI"));
        await _downloadHelper.DownloadFileAsync(downloadUrl, destPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.WriteLine();

        string exePath = destPath;
        string? workingDir = tempDir;
        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Extracting...[/]");
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(destPath, extractDir, overwriteFiles: true);
            var exeInDir = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exeInDir != null)
            {
                exePath = exeInDir;
                workingDir = Path.GetDirectoryName(exePath);
            }
        }

        if (string.IsNullOrEmpty(workingDir))
            workingDir = Path.GetDirectoryName(exePath);

        // Resolve starter project path (must be cloned first for integrate-ue)
        var projectDir = ProjectPathHelper.ResolveStarterProjectPath(_options);
        if (string.IsNullOrEmpty(projectDir))
            return false;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine, SmehState.StepStarterProject }, projectDir, string.IsNullOrEmpty(cssPath) ? null : cssPath))
            return false;

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!ProjectPathHelper.TryPromptProjectPath(out var promptedDir, out var promptedUproject))
                return false;
            projectDir = promptedDir!;
            uprojectPath = promptedUproject!;
            SmehState.SetLastClonePath(projectDir);
        }

        // 1. Download SDK
        var sdkVersion = _options.SdkVersion.Trim();
        if (string.IsNullOrEmpty(sdkVersion))
            sdkVersion = "2023.1.3.8471";
        var downloadArgs = $"download --sdk-version \"{sdkVersion}\" --filter Packages=SDK --filter DeploymentPlatforms=Windows_vc160 --filter DeploymentPlatforms=Windows_vc170 --filter DeploymentPlatforms=Linux --filter DeploymentPlatforms= --email \"\" --password \"\"";
        AnsiConsole.MarkupLine($"[dim]Running: wwise-cli download ... (output below)[/]");
        var downloadResult = await _processRunner.RunWithConsoleOutputAsync(exePath, downloadArgs, workingDir, waitForExit: true, sendInputWhenLine: line =>
        {
            if (line != null && (line.Contains("Enter Wwise email:", StringComparison.OrdinalIgnoreCase) || line.Contains("Enter Wwise password:", StringComparison.OrdinalIgnoreCase)))
                return "";
            return null;
        });
        if (downloadResult.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Download step failed (exit code {downloadResult.ExitCode}).[/]");
            if (!string.IsNullOrEmpty(downloadResult.StdError))
                AnsiConsole.WriteLine(downloadResult.StdError);
            if (!string.IsNullOrEmpty(downloadResult.StdOut))
                AnsiConsole.WriteLine(downloadResult.StdOut);
            return false;
        }
        AnsiConsole.MarkupLine("[green]SDK download completed.[/]");

        // 2. Integrate with Unreal project
        var integrationVersion = _options.IntegrationVersion.Trim();
        if (string.IsNullOrEmpty(integrationVersion))
            integrationVersion = "2023.1.3.2970";
        var integrateArgs = $"integrate-ue --email \"\" --password \"\" --integration-version \"{integrationVersion}\" --project \"{uprojectPath}\"";
        AnsiConsole.MarkupLine($"[dim]Running: wwise-cli integrate-ue ... (output below)[/]");
        // Attach stdin to console so the child gets a real console and avoids 'The handle is invalid' from some tools when stdin is redirected.
        var integrateResult = await _processRunner.RunWithConsoleOutputAsync(exePath, integrateArgs, workingDir, waitForExit: true, attachStdinToConsole: true, heartbeatInterval: TimeSpan.FromSeconds(30), heartbeatMessage: "Integrate step still running...");
        if (integrateResult.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Integrate step failed (exit code {integrateResult.ExitCode}).[/]");
            if (!string.IsNullOrEmpty(integrateResult.StdError))
                AnsiConsole.WriteLine(integrateResult.StdError);
            if (!string.IsNullOrEmpty(integrateResult.StdOut))
                AnsiConsole.WriteLine(integrateResult.StdOut);
            return false;
        }
        AnsiConsole.MarkupLine("[green]Wwise integration completed successfully.[/]");
        return true;
    }

}
