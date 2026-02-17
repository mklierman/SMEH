using System.Net.Http.Headers;
using System.Text.Json;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

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

    public async Task RunAsync()
    {
        var repo = _options.Repository.Trim();
        if (string.IsNullOrEmpty(repo))
        {
            Console.WriteLine("WwiseCli:Repository is not configured in appsettings.json.");
            return;
        }

        string releaseUrl = _options.UseLatest
            ? $"https://api.github.com/repos/{repo}/releases/latest"
            : $"https://api.github.com/repos/{repo}/releases/tags/{_options.ReleaseTag}";

        Console.WriteLine("Fetching release info...");
        using var response = await HttpClient.GetAsync(releaseUrl);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get release: {response.StatusCode}. Check Repository and ReleaseTag in appsettings.json.");
            return;
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
            Console.WriteLine("No Windows executable or zip found in this release.");
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "WwiseCLI");
        Directory.CreateDirectory(tempDir);
        var destPath = Path.Combine(tempDir, assetName);

        Console.WriteLine($"Downloading {assetName}...");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Wwise-CLI"));
        await _downloadHelper.DownloadFileAsync(downloadUrl, destPath, progress);
        ConsoleProgressBar.Clear();
        Console.WriteLine();

        string exePath = destPath;
        string? workingDir = tempDir;
        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Extracting...");
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
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine, SmehState.StepStarterProject }, projectDir, string.IsNullOrEmpty(cssPath) ? null : cssPath))
            return;

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            Console.WriteLine($"FactoryGame.uproject not found at: {uprojectPath}");
            Console.WriteLine("Ensure the Starter Project (option 4) is cloned and contains FactoryGame.uproject.");
            return;
        }

        // 1. Download SDK
        var sdkVersion = _options.SdkVersion.Trim();
        if (string.IsNullOrEmpty(sdkVersion))
            sdkVersion = "2023.1.3.8471";
        var downloadArgs = $"download --sdk-version \"{sdkVersion}\" --filter Packages=SDK --filter DeploymentPlatforms=Windows_vc160 --filter DeploymentPlatforms=Windows_vc170";
        Console.WriteLine("Running: wwise-cli download ...");
        var downloadResult = await _processRunner.RunAsync(exePath, downloadArgs, workingDir, waitForExit: true);
        if (downloadResult.ExitCode != 0)
        {
            Console.WriteLine($"Download step failed (exit code {downloadResult.ExitCode}).");
            if (!string.IsNullOrEmpty(downloadResult.StdError))
                Console.WriteLine(downloadResult.StdError);
            if (!string.IsNullOrEmpty(downloadResult.StdOut))
                Console.WriteLine(downloadResult.StdOut);
            return;
        }
        Console.WriteLine("SDK download completed.");

        // 2. Integrate with Unreal project
        var integrationVersion = _options.IntegrationVersion.Trim();
        if (string.IsNullOrEmpty(integrationVersion))
            integrationVersion = "2023.1.3.2970";
        var integrateArgs = $"integrate-ue --integration-version \"{integrationVersion}\" --project \"{uprojectPath}\"";
        Console.WriteLine("Running: wwise-cli integrate-ue ...");
        var integrateResult = await _processRunner.RunAsync(exePath, integrateArgs, workingDir, waitForExit: true);
        if (integrateResult.ExitCode != 0)
        {
            Console.WriteLine($"Integrate step failed (exit code {integrateResult.ExitCode}).");
            if (!string.IsNullOrEmpty(integrateResult.StdError))
                Console.WriteLine(integrateResult.StdError);
            if (!string.IsNullOrEmpty(integrateResult.StdOut))
                Console.WriteLine(integrateResult.StdOut);
            return;
        }
        Console.WriteLine("Wwise integration completed successfully.");
    }

    private string? ResolveStarterProjectPath()
    {
        var path = _options.StarterProjectPath?.Trim();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = SmehState.GetLastClonePath();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        Console.WriteLine("Starter project path not found. Run option 4 (Starter Project) first to clone the repo,");
        Console.WriteLine("or set WwiseCli:StarterProjectPath in appsettings.json to the clone directory.");
        Console.Write("Enter path to SatisfactoryModLoader clone now (or press Enter to cancel): ");
        path = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine("Cancelled.");
            return null;
        }
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Directory not found: {path}");
            return null;
        }
        return path;
    }
}
