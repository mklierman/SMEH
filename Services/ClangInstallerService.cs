using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

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

    public async Task RunAsync()
    {
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio }))
            return;

        if (string.IsNullOrWhiteSpace(_options.InstallerUrl))
        {
            AnsiConsole.MarkupLine("[red]Clang installer URL is not configured in appsettings.json.[/]");
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "Clang");
        var installerPath = Path.Combine(tempDir, "v22_clang-16.0.6-centos7.exe");

        AnsiConsole.MarkupLine("[dim]Downloading Clang installer...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "Clang"));
        await _downloadHelper.DownloadFileAsync(_options.InstallerUrl, installerPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete. Running installer...[/]");

        var result = await _processRunner.RunAsync(installerPath, null, null, waitForExit: true);
        if (result.ExitCode != 0 && !string.IsNullOrEmpty(result.StdError))
            AnsiConsole.WriteLine("Stderr: " + result.StdError);

        if (result.ExitCode == 0)
        {
            AnsiConsole.MarkupLine("[green]Clang installer finished successfully.[/]");
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
        else
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
    }
}
