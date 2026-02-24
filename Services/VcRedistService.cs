using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Installs Visual C++ Redistributable (used as a prerequisite by CSS Unreal Engine installer).</summary>
public class VcRedistService
{
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public VcRedistService(DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        if (IsVcRedistInstalled())
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Visual C++ Redistributable 2015-2022 (x64) is already installed. Skipping.[/]");
            return true;
        }

        var tempDir = Path.Combine(CleanupService.TempRoot, "VcRedist");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, "vc_redist.x64.exe");

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Downloading Visual C++ Redistributable 2015-2022 (x64)...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "VC++"));
        try
        {
            await _downloadHelper.DownloadFileAsync(AppDefaults.VcRedistX64Url, installerPath, progress);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Download failed: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete. Running installer (quiet mode)...[/]");
        var alreadyElevated = ProcessRunner.IsRunningElevated();
        if (OperatingSystem.IsWindows() && !alreadyElevated)
            AnsiConsole.MarkupLine($"[dim]You may see a UAC prompt to allow administrator access.[/]");

        // /install /quiet /norestart - silent install. Run elevated on Windows unless already admin.
        var args = "/install /quiet /norestart";
        var result = OperatingSystem.IsWindows() && !alreadyElevated
            ? await _processRunner.RunElevatedAsync(installerPath, args, tempDir, waitForExit: true)
            : await _processRunner.RunAsync(installerPath, args, tempDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}. You can try running it manually: {Markup.Escape(installerPath)}[/]");
            return false;
        }
        AnsiConsole.MarkupLine("[green]Visual C++ Redistributable installed successfully.[/]");
        try
        {
            if (File.Exists(installerPath))
                File.Delete(installerPath);
        }
        catch
        {
            AnsiConsole.MarkupLineInterpolated($"[dim]Left installer in: {Markup.Escape(tempDir)}[/]");
        }
        return true;
    }

    /// <summary>Returns true if VC++ 2015-2022 runtime (vcruntime140.dll, MSVCP140.dll) appears to be installed.</summary>
    private static bool IsVcRedistInstalled()
    {
        var systemDir = Environment.SystemDirectory;
        return File.Exists(Path.Combine(systemDir, "vcruntime140.dll"))
            || File.Exists(Path.Combine(systemDir, "MSVCP140.dll"));
    }
}
