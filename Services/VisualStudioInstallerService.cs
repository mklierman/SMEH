using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Downloads and runs the Visual Studio 2022 installer with the configured workload</summary>
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

    public async Task<bool> RunAsync()
    {
        var tempDir = Path.Combine(CleanupService.TempRoot, "VS2022");
        Directory.CreateDirectory(tempDir);
        var configPath = await GetConfigPathAsync(tempDir);
        var hasConfig = !string.IsNullOrEmpty(configPath) && File.Exists(configPath);

        if (SmehState.TryGetVisualStudio2022InstallPath(out var installationPath) && !string.IsNullOrEmpty(installationPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Visual Studio 2022 is already installed at: {Markup.Escape(installationPath)}[/]");
            if (!hasConfig)
            {
                AnsiConsole.MarkupLine("[yellow]No .vsconfig file was found or downloaded, so there is nothing to apply.[/]");
                return true;
            }

            return await ApplyConfigToExistingInstallAsync(installationPath, configPath!, tempDir);
        }

        return await InstallCommunityAsync(tempDir, configPath, hasConfig);
    }

    private async Task<string?> GetConfigPathAsync(string tempDir)
    {
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
                configFileName = AppDefaults.VisualStudioConfigFileName;
            configPath = Path.Combine(tempDir, configFileName);
            AnsiConsole.MarkupLine($"[dim]Downloading Visual Studio config (SML workload)...[/]");
            var configProgress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Config"));
            await _downloadHelper.DownloadFileAsync(_options.ConfigFileUrl, configPath, configProgress);
            ConsoleProgressBar.Clear();
            AnsiConsole.MarkupLine("[green]Config download complete.[/]");
        }

        return configPath;
    }

    private async Task<bool> InstallCommunityAsync(string tempDir, string? configPath, bool hasConfig)
    {
        var bootstrapperPath = Path.Combine(tempDir, AppDefaults.VisualStudioBootstrapperFileName);

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing Visual Studio 2022 Community Edition (free).[/]");
        AnsiConsole.MarkupLine($"[dim]Downloading bootstrapper...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Bootstrapper"));
        await _downloadHelper.DownloadFileAsync(AppDefaults.VisualStudioBootstrapperUrl, bootstrapperPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete.[/]");

        var arguments = "--passive --wait --norestart";
        if (hasConfig)
            arguments = $"--config \"{configPath}\" {arguments}";

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Running Visual Studio installer (this may take a long time)...[/]");
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

    private async Task<bool> ApplyConfigToExistingInstallAsync(string installationPath, string configPath, string tempDir)
    {
        var installerPath = GetVisualStudioInstallerPath();
        if (!File.Exists(installerPath))
        {
            AnsiConsole.MarkupLine("[yellow]Visual Studio Installer was not found. Downloading the VS 2022 Community bootstrapper to apply the config.[/]");
            installerPath = Path.Combine(tempDir, AppDefaults.VisualStudioBootstrapperFileName);
            var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Bootstrapper"));
            await _downloadHelper.DownloadFileAsync(AppDefaults.VisualStudioBootstrapperUrl, installerPath, progress);
            ConsoleProgressBar.Clear();
            AnsiConsole.MarkupLine("[green]Download complete.[/]");
        }

        var arguments = $"modify --installPath \"{installationPath}\" --config \"{configPath}\" --passive --wait --norestart";
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Applying Visual Studio config (SML workload) to the existing installation...[/]");
        var result = await _processRunner.RunAsync(installerPath, arguments, tempDir, waitForExit: true);

        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Visual Studio config apply exited with code {result.ExitCode}.[/]");
            if (!string.IsNullOrEmpty(result.StdError))
                AnsiConsole.WriteLine("Stderr: " + result.StdError);
            return false;
        }

        AnsiConsole.MarkupLine("[green]Visual Studio config applied successfully.[/]");
        return true;
    }

    private static string GetVisualStudioInstallerPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            AppDefaults.VisualStudioInstallerFileName);
    }
}
