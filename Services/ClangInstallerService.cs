using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Downloads and installs the Clang cross-toolchain used by Unreal Engine; menu option 4.</summary>
public class ClangInstallerService
{
    private readonly ClangOptions _options;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public ClangInstallerService(ClangOptions options, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio }))
            return false;

        var installerUrlOrPath = _options.InstallerUrl?.Trim();
        if (string.IsNullOrWhiteSpace(installerUrlOrPath))
        {
            AnsiConsole.MarkupLine("[red]Clang installer URL is not configured.[/]");
            installerUrlOrPath = AnsiConsole.Prompt(new TextPrompt<string>("Enter Clang installer URL or path to local .exe (or press Enter to cancel):")
                .AllowEmpty());
            if (string.IsNullOrWhiteSpace(installerUrlOrPath))
            {
                AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
                return false;
            }
            installerUrlOrPath = installerUrlOrPath!.Trim();
        }

        string installerPath;
        if (installerUrlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || installerUrlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var tempDir = Path.Combine(CleanupService.TempRoot, "Clang");
            installerPath = Path.Combine(tempDir, "v22_clang-16.0.6-centos7.exe");
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Downloading Clang installer...[/]");
            var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Clang"));
            await _downloadHelper.DownloadFileAsync(installerUrlOrPath, installerPath, progress);
            ConsoleProgressBar.Clear();
            AnsiConsole.MarkupLine("[green]Download complete. Running installer...[/]");
        }
        else
        {
            if (!File.Exists(installerUrlOrPath))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]File not found: {Markup.Escape(installerUrlOrPath)}[/]");
                return false;
            }
            installerPath = installerUrlOrPath;
            AnsiConsole.MarkupLine("[green]Running local installer...[/]");
        }

        var result = await _processRunner.RunAsync(installerPath, "/S", null, waitForExit: true);
        if (result.ExitCode != 0 && !string.IsNullOrEmpty(result.StdError))
            AnsiConsole.WriteLine("Stderr: " + result.StdError);

        if (result.ExitCode == 0)
        {
            AnsiConsole.MarkupLine("[green]Clang installer finished successfully.[/]");
            var isDownloaded = installerUrlOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || installerUrlOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            if (isDownloaded)
            {
                try
                {
                    if (File.Exists(installerPath))
                        File.Delete(installerPath);
                }
                catch
                {
                    AnsiConsole.MarkupLineInterpolated($"[yellow]Could not delete downloaded installer. You may remove it manually: {Markup.Escape(installerPath)}[/]");
                }
            }
        }
        else
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
        return result.ExitCode == 0;
    }
}
