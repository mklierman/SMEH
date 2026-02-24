using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Clones the SatisfactoryModLoader (SML) repo and optionally runs Wwise integration; menu option 5.</summary>
public class StarterProjectService
{
    private readonly StarterProjectOptions _options;
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("SMEH", "1.0") } }
    };

    public StarterProjectService(StarterProjectOptions options, CssUnrealEngineOptions cssUnrealEngineOptions, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine }, cssUnrealEnginePath: string.IsNullOrEmpty(cssPath) ? null : cssPath))
            return false;

        string basePath;
        if (SmehState.RunAllUnattended && !string.IsNullOrWhiteSpace(_options.DefaultClonePath?.Trim()))
        {
            basePath = _options.DefaultClonePath.Trim();
        }
        else
        {
            basePath = AnsiConsole.Prompt(new TextPrompt<string>("Enter install location for the starter project (e.g. C:\\Modding):"));
            if (string.IsNullOrWhiteSpace(basePath?.Trim()))
            {
                AnsiConsole.MarkupLine("[red]No path entered. Aborted.[/]");
                return false;
            }
            basePath = basePath!.Trim();
        }
        if (!Directory.Exists(basePath))
        {
            try
            {
                Directory.CreateDirectory(basePath);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Could not create directory: {Markup.Escape(ex.Message)}[/]");
                return false;
            }
        }
        var targetPath = Path.Combine(basePath, "SatisfactoryModLoader");

        if (Directory.Exists(targetPath))
        {
            var hasGit = Directory.Exists(Path.Combine(targetPath, ".git"));
            if (hasGit)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Directory already exists and appears to be a git clone: {Markup.Escape(targetPath)}[/]");
                AnsiConsole.MarkupLine("[yellow]Choose a different path or remove the existing folder.[/]");
                return false;
            }
        }

        var gitPath = GetGitPath();
        if (string.IsNullOrEmpty(gitPath))
        {
            AnsiConsole.MarkupLine("[yellow]Git was not found on PATH.[/]");
            if (!SmehState.RunAllUnattended)
            {
                var install = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Install Git automatically?")
                    .HighlightStyle(SmehTheme.AccentStyle)
                    .AddChoices("Yes", "No"));
                if (install != "Yes")
                {
                    AnsiConsole.MarkupLine("[yellow]Please install Git from [link=https://git-scm.com/download/win]git-scm.com[/] and ensure it is in your PATH.[/]");
                    return false;
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing Git.[/]");
            }
            var installed = await InstallGitAsync();
            if (!installed)
            {
                AnsiConsole.MarkupLine("[red]Git install failed or was cancelled. Please install Git manually and run this option again.[/]");
                return false;
            }
            gitPath = GetGitPath();
            if (string.IsNullOrEmpty(gitPath))
            {
                AnsiConsole.MarkupLine("[yellow]Git was installed but could not be found. Try opening a new terminal or run this option again.[/]");
                return false;
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[{SmehTheme.FicsitOrange}]Cloning {Markup.Escape(_options.RepositoryUrl)} (branch: {Markup.Escape(_options.Branch)}) to {Markup.Escape(targetPath)}...[/]");
        var args = $"clone --branch \"{_options.Branch}\" \"{_options.RepositoryUrl}\" \"{targetPath}\"";
        var result = await _processRunner.RunAsync(gitPath, args, null, waitForExit: true);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]Clone failed.[/]");
            if (!string.IsNullOrEmpty(result.StdError))
                AnsiConsole.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                AnsiConsole.WriteLine(result.StdOut);
            return false;
        }

        SmehState.SetLastClonePath(targetPath);
        AnsiConsole.MarkupLineInterpolated($"[green]Successfully cloned to {Markup.Escape(targetPath)}[/]");
        return true;
    }

    private static string? GetGitPath()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            p?.WaitForExit(5000);
            if (p != null && p.ExitCode == 0)
                return "git";
        }
        catch
        {
            // fall through
        }
        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe");
        if (File.Exists(defaultPath))
            return defaultPath;
        return null;
    }

    private async Task<bool> InstallGitAsync()
    {
        const string repo = "git-for-windows/git";
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Fetching Git for Windows latest release...[/]");
        using var response = await HttpClient.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
        if (!response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to get release info: {response.StatusCode}[/]");
            return false;
        }
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var assets = doc.RootElement.GetProperty("assets");
        string? downloadUrl = null;
        string? assetName = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains("64-bit", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                assetName = name;
                break;
            }
        }
        if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(assetName))
        {
            AnsiConsole.MarkupLine("[red]No 64-bit Git installer found in release.[/]");
            return false;
        }
        var tempDir = Path.Combine(CleanupService.TempRoot, "GitInstall");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, assetName);
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Downloading Git installer...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Git"));
        try
        {
            await _downloadHelper.DownloadFileAsync(downloadUrl, installerPath, progress);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Download failed: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing Git (this may take a minute)...[/]");
        var result = await _processRunner.RunAsync(installerPath, "/VERYSILENT /NORESTART", tempDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}[/]");
            return false;
        }
        AnsiConsole.MarkupLine("[green]Git installed.[/]");
        return true;
    }
}
