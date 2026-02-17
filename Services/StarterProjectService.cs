using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

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

    public async Task RunAsync()
    {
        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine }, cssUnrealEnginePath: string.IsNullOrEmpty(cssPath) ? null : cssPath))
            return;

        Console.Write("Enter install location for the starter project (e.g. C:\\Modding): ");
        var basePath = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(basePath))
        {
            Console.WriteLine("No path entered. Aborted.");
            return;
        }
        if (!Directory.Exists(basePath))
        {
            try
            {
                Directory.CreateDirectory(basePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not create directory: {ex.Message}");
                return;
            }
        }
        var targetPath = Path.Combine(basePath, "SatisfactoryModLoader");

        if (Directory.Exists(targetPath))
        {
            var hasGit = Directory.Exists(Path.Combine(targetPath, ".git"));
            if (hasGit)
            {
                Console.WriteLine($"Directory already exists and appears to be a git clone: {targetPath}");
                Console.WriteLine("Choose a different path or remove the existing folder.");
                return;
            }
        }

        var gitPath = GetGitPath();
        if (string.IsNullOrEmpty(gitPath))
        {
            Console.WriteLine("Git was not found on PATH.");
            Console.Write("Install Git automatically? (y/n): ");
            var install = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (install != "Y" && install != "YES")
            {
                Console.WriteLine("Please install Git from https://git-scm.com/download/win and ensure it is in your PATH.");
                return;
            }
            var installed = await InstallGitAsync();
            if (!installed)
            {
                Console.WriteLine("Git install failed or was cancelled. Please install Git manually and run this option again.");
                return;
            }
            gitPath = GetGitPath();
            if (string.IsNullOrEmpty(gitPath))
            {
                Console.WriteLine("Git was installed but could not be found. Try opening a new terminal or run this option again.");
                return;
            }
        }

        Console.WriteLine($"Cloning {_options.RepositoryUrl} (branch: {_options.Branch}) to {targetPath}...");
        var args = $"clone --branch \"{_options.Branch}\" \"{_options.RepositoryUrl}\" \"{targetPath}\"";
        var result = await _processRunner.RunAsync(gitPath, args, null, waitForExit: true);

        if (result.ExitCode != 0)
        {
            Console.WriteLine("Clone failed.");
            if (!string.IsNullOrEmpty(result.StdError))
                Console.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                Console.WriteLine(result.StdOut);
            return;
        }

        SmehState.SetLastClonePath(targetPath);
        Console.WriteLine($"Successfully cloned to {targetPath}");
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
        Console.WriteLine("Fetching Git for Windows latest release...");
        using var response = await HttpClient.GetAsync($"https://api.github.com/repos/{repo}/releases/latest");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to get release info: {response.StatusCode}");
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
            Console.WriteLine("No 64-bit Git installer found in release.");
            return false;
        }
        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "GitInstall");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, assetName);
        Console.WriteLine("Downloading Git installer...");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Git"));
        try
        {
            await _downloadHelper.DownloadFileAsync(downloadUrl, installerPath, progress);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Download failed: {ex.Message}");
            return false;
        }
        ConsoleProgressBar.Clear();
        Console.WriteLine("Installing Git (this may take a minute)...");
        var result = await _processRunner.RunAsync(installerPath, "/VERYSILENT /NORESTART", tempDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"Installer exited with code {result.ExitCode}");
            return false;
        }
        Console.WriteLine("Git installed.");
        return true;
    }
}
