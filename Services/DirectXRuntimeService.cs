using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

public class DirectXRuntimeService
{
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public DirectXRuntimeService(DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        if (IsDirectXEndUserRuntimeInstalled())
        {
            AnsiConsole.MarkupLine("[dim]DirectX End-User Runtime is already installed. Skipping.[/]");
            return true;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "DirectX");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, "dxwebsetup.exe");

        AnsiConsole.MarkupLine("[dim]Downloading DirectX End-User Runtime Web Installer...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "DirectX"));
        try
        {
            await _downloadHelper.DownloadFileAsync(AppDefaults.DirectXWebInstallerUrl, installerPath, progress);
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
            AnsiConsole.MarkupLine("[dim]You may see a UAC prompt to allow administrator access. This is required for DirectX install.[/]");
        AnsiConsole.MarkupLine("[dim]This may take a few minutes. The installer may show a progress window.[/]");

        // /Q = quiet install. On Windows, run elevated unless we're already admin (then child inherits; no second UAC).
        var result = OperatingSystem.IsWindows() && !alreadyElevated
            ? await _processRunner.RunElevatedAsync(installerPath, "/Q", tempDir, waitForExit: true)
            : await _processRunner.RunAsync(installerPath, "/Q", tempDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}. You can try running it manually: {Markup.Escape(installerPath)}[/]");
            return false;
        }
        AnsiConsole.MarkupLine("[green]DirectX End-User Runtime installed successfully.[/]");
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

    /// <summary>Returns true if XINPUT1_3.dll (from DirectX End-User Runtime) is present in system directories.</summary>
    private static bool IsDirectXEndUserRuntimeInstalled()
    {
        var systemDir = Environment.SystemDirectory;
        var sysWoW64 = Path.Combine(Path.GetPathRoot(systemDir) ?? "C:", "Windows", "SysWOW64");
        return File.Exists(Path.Combine(systemDir, "XINPUT1_3.dll"))
            || File.Exists(Path.Combine(sysWoW64, "XINPUT1_3.dll"));
    }
}
