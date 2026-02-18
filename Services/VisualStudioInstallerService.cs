using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

public class VisualStudioInstallerService
{
    private readonly VisualStudioOptions _options;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public VisualStudioInstallerService(VisualStudioOptions options, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    /// <summary>Official Community Edition bootstrapper (free). Used so we always install Community regardless of config.</summary>
    private const string CommunityBootstrapperUrl = "https://aka.ms/vs/17/release/vs_community.exe";

    public async Task<bool> RunAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "VS2022");
        Directory.CreateDirectory(tempDir);
        var bootstrapperPath = Path.Combine(tempDir, "vs_community.exe");

        AnsiConsole.MarkupLine("[dim]Installing Visual Studio 2022 Community Edition (free).[/]");
        AnsiConsole.MarkupLine("[dim]Downloading bootstrapper...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Bootstrapper"));
        await _downloadHelper.DownloadFileAsync(CommunityBootstrapperUrl, bootstrapperPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete.[/]");

        string? configPath = null;
        var localPath = _options.ConfigFilePath?.Trim();
        if (!string.IsNullOrEmpty(localPath))
        {
            configPath = Path.IsPathRooted(localPath) ? localPath : Path.Combine(AppContext.BaseDirectory, localPath);
            if (!File.Exists(configPath))
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Config file not found: {Markup.Escape(_options.ConfigFilePath ?? "")}. Will try config URL if set.[/]");
                configPath = null;
            }
        }

        if (configPath == null && !string.IsNullOrWhiteSpace(_options.ConfigFileUrl))
        {
            var configFileName = Path.GetFileName(new Uri(_options.ConfigFileUrl).LocalPath);
            if (string.IsNullOrEmpty(configFileName))
                configFileName = "SML.vsconfig";
            configPath = Path.Combine(tempDir, configFileName);
            AnsiConsole.MarkupLine("[dim]Downloading Visual Studio config (SML workload)...[/]");
            var configProgress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Config"));
            await _downloadHelper.DownloadFileAsync(_options.ConfigFileUrl, configPath, configProgress);
            ConsoleProgressBar.Clear();
            AnsiConsole.MarkupLine("[green]Config download complete.[/]");
        }

        var hasConfig = !string.IsNullOrEmpty(configPath) && File.Exists(configPath);

        // Install: --passive (no interactive prompts), --wait (wait for exit), --norestart
        var arguments = "--passive --wait --norestart";
        if (hasConfig)
            arguments = $"--config \"{configPath}\" {arguments}";

        AnsiConsole.MarkupLine("[dim]Running Visual Studio installer (this may take a long time)...[/]");
        var result = await _processRunner.RunAsync(bootstrapperPath, arguments, tempDir, waitForExit: true);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
            if (!string.IsNullOrEmpty(result.StdError))
                AnsiConsole.WriteLine("Stderr: " + result.StdError);
            return false;
        }

        AnsiConsole.MarkupLine("[green]Visual Studio 2022 installation finished successfully.[/]");
        return true;
    }
}
