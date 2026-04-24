using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Downloads and installs the CSS (Custom Source Satisfactory) Unreal Engine build; menu option 2. Runs DirectX and VC Redist installers as prerequisites.</summary>
public class CssUnrealEngineService
{
    private readonly CssUnrealEngineOptions _options;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public CssUnrealEngineService(CssUnrealEngineOptions options, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    private const string UnrealEngineInstallerArgs = "/SILENT /NORESTART";
    private static readonly string DefaultInstallPath = AppDefaults.CssUnrealEngineInstallPath;
    private const string ManualInstallExe = "UnrealEngine-CSS-Editor-Win64.exe";
    private const string ManualInstallBin1 = "UnrealEngine-CSS-Editor-Win64-1.bin";
    private const string ManualInstallBin2 = "UnrealEngine-CSS-Editor-Win64-2.bin";

    private async Task RunDirectXAndVcRedistAsync()
    {
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing DirectX End-User Runtime (required for Unreal Engine)...[/]");
        var directXService = new DirectXRuntimeService(_downloadHelper, _processRunner);
        if (!await directXService.RunAsync())
            AnsiConsole.MarkupLine("[yellow]DirectX install failed or was skipped. Unreal Engine may show XINPUT1_3.dll errors. Continuing with UE install.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing Visual C++ Redistributable 2015-2022 (x64) (required for Unreal Engine)...[/]");
        var vcRedistService = new VcRedistService(_downloadHelper, _processRunner);
        if (!await vcRedistService.RunAsync())
            AnsiConsole.MarkupLine("[yellow]VC++ Redist install failed or was skipped. Continuing with UE install.[/]");
        AnsiConsole.WriteLine();
    }

    public async Task<bool> RunAsync()
    {
        var repo = _options.Repository?.Trim();
        if (string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]CSS Unreal Engine: Repository is not set.[/]");
            repo = AnsiConsole.Prompt(new TextPrompt<string>("Enter GitHub repository (e.g. satisfactorymodding/UnrealEngine) (or press Enter to cancel):")
                .AllowEmpty());
            if (string.IsNullOrWhiteSpace(repo))
            {
                AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
                return false;
            }
            repo = repo.Trim();
        }

        if (string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]Repository is required.[/]");
            return false;
        }

        // Custom Unreal Engine is in a private repo; user must complete linking first.
        const string linkingDocsUrl = "https://docs.ficsit.app/satisfactory-modding/latest/Development/BeginnersGuide/dependencies.html#_link_your_github_as_an_epic_games_developer_account";
        AnsiConsole.MarkupLine("The Custom Unreal Engine is in a [yellow]private repository[/]. Before continuing you must:");
        AnsiConsole.MarkupLine("  1. Link your GitHub account as an Epic Games developer account");
        AnsiConsole.MarkupLine("  2. Link your GitHub account to the Satisfactory Modding repository (Unreal Linker)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"Full instructions: [link={linkingDocsUrl}]documentation[/]");
        AnsiConsole.WriteLine();
        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Have you completed the linking process?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (confirm != "Yes")
        {
            AnsiConsole.MarkupLine("[yellow]Please complete the steps at the link above, then run this option again.[/]");
            return false;
        }

        AnsiConsole.WriteLine();
        var releasesUrl = $"https://github.com/{repo}/releases/latest";
        AnsiConsole.MarkupLine("Download and install the engine yourself:");
        AnsiConsole.MarkupLineInterpolated($"  1. Open: [link={releasesUrl}]releases[/]");
        AnsiConsole.MarkupLine("  2. Download the .exe and .bin files (e.g. UnrealEngine-CSS-Editor-Win64.exe and its .bin parts).");
        AnsiConsole.WriteLine();
        var runNow = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Have you downloaded all the files? Do you want to run the installer?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (runNow == "No")
            return false;
        var installFolder = GetManualInstallFolder();
        if (string.IsNullOrEmpty(installFolder))
            return false;
        await RunDirectXAndVcRedistAsync();
        return await RunInstallerFromFolderAsync(installFolder);
    }

    /// <summary>Prompts for install location (default or custom). Sets <see cref="CssUnrealEngineOptions.InstallPath"/> and returns true if a path was chosen, false if cancelled.</summary>
    private bool PromptInstallPath()
    {
        if (SmehState.RunAllUnattended && !string.IsNullOrWhiteSpace(_options.InstallPath))
            return true;
        AnsiConsole.MarkupLineInterpolated($"[dim]Default install location: [white]{Markup.Escape(DefaultInstallPath)}[/][/]");
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Use this location or choose a custom path?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Use default", "Custom path"));
        if (choice == "Use default")
        {
            _options.InstallPath = DefaultInstallPath;
            return true;
        }
        var customPath = AnsiConsole.Prompt(new TextPrompt<string>("Enter custom install path (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(customPath))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
            return false;
        }
        customPath = customPath!.Trim();
        if (!Directory.Exists(Path.GetPathRoot(customPath) ?? ""))
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Drive or root not found. Installation may create the folder: {Markup.Escape(customPath)}[/]");
        }
        _options.InstallPath = customPath;
        return true;
    }

    private static string GetInstallerArgs(string installPath)
    {
        var args = UnrealEngineInstallerArgs;
        if (!string.Equals(installPath.Trim(), DefaultInstallPath, StringComparison.OrdinalIgnoreCase))
            args += $" /DIR=\"{installPath.Trim()}\"";
        return args;
    }

    /// <summary>Asks if the user wants to delete Unreal Engine installer files (exe + .bin). Uses LastUnrealEngineInstallerFolder when set (run-all Manual path); otherwise skips. Used at end of run-all.</summary>
    public static void OfferToDeleteEngineInstallerFiles()
    {
        var folder = SmehState.LastUnrealEngineInstallerFolder?.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            SmehState.LastUnrealEngineInstallerFolder = null;
            return;
        }
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"Delete Unreal Engine installer files from {Markup.Escape(folder)}?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        SmehState.LastUnrealEngineInstallerFolder = null;
        if (choice != "Yes")
            return;
        var filePaths = new[] { Path.Combine(folder, ManualInstallExe), Path.Combine(folder, ManualInstallBin1), Path.Combine(folder, ManualInstallBin2) };
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    AnsiConsole.MarkupLineInterpolated($"[dim]Deleted {Markup.Escape(Path.GetFileName(path))}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Could not delete {Markup.Escape(path)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }

    /// <summary>Offers to delete the given installer files. Deletes only if user chooses Yes.</summary>
    private static void OfferToDeleteInstallerFiles(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return;
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Delete installer files?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (choice != "Yes")
            return;
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    AnsiConsole.MarkupLineInterpolated($"[dim]Deleted {Markup.Escape(Path.GetFileName(path))}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Could not delete {Markup.Escape(path)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }

    /// <summary>Returns true if folder contains the exact exe and both .bin files from the manual install set.</summary>
    private static bool HasManualInstallFiles(string folder, out string? exePath)
    {
        exePath = null;
        var exe = Path.Combine(folder, ManualInstallExe);
        var bin1 = Path.Combine(folder, ManualInstallBin1);
        var bin2 = Path.Combine(folder, ManualInstallBin2);
        if (!File.Exists(exe) || !File.Exists(bin1) || !File.Exists(bin2))
            return false;
        exePath = exe;
        return true;
    }

    /// <summary>Gets the folder containing the manually downloaded installer: tries Downloads first, then prompts.</summary>
    private static string? GetManualInstallFolder()
    {
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloadsPath) && HasManualInstallFiles(downloadsPath, out _))
        {
            var useDownloads = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"Found installer in Downloads folder. Use [{SmehTheme.FicsitOrange}]{Markup.Escape(downloadsPath)}[/]?")
                .HighlightStyle(SmehTheme.AccentStyle)
                .AddChoices("Yes", "No, choose another folder"));
            if (useDownloads == "Yes")
                return downloadsPath;
        }

        var folder = AnsiConsole.Prompt(new TextPrompt<string>("Files not found in Downloads. Enter the folder path where you saved the UnrealEngine-CSS-Editor-Win64 files:")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(folder))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
            return null;
        }
        folder = folder.Trim();
        if (!Directory.Exists(folder))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Folder not found: {Markup.Escape(folder)}[/]");
            return null;
        }
        if (!HasManualInstallFiles(folder, out _))
        {
            AnsiConsole.MarkupLine($"[red]Folder must contain exactly: [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64.exe[/], [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64-1.bin[/], and [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64-2.bin[/].[/]");
            return null;
        }
        return folder;
    }

    private async Task<bool> RunInstallerFromFolderAsync(string folder)
    {
        if (SmehState.RunAllUnattended)
            SmehState.LastUnrealEngineInstallerFolder = folder;
        if (!HasManualInstallFiles(folder, out var exePath) || exePath == null)
        {
            AnsiConsole.MarkupLine("[red]Installer files not found.[/]");
            return false;
        }
        AnsiConsole.MarkupLineInterpolated($"[dim]Running installer: {Markup.Escape(ManualInstallExe)}[/]");
        if (!PromptInstallPath())
            return false;
        var result = await _processRunner.RunAsync(exePath, GetInstallerArgs(_options.InstallPath), folder, waitForExit: true);
        if (result.ExitCode != 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
        else
        {
            AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
            if (!SmehState.RunAllUnattended)
                OfferToDeleteInstallerFiles(new[]
                {
                    Path.Combine(folder, ManualInstallExe),
                    Path.Combine(folder, ManualInstallBin1),
                    Path.Combine(folder, ManualInstallBin2)
                });
        }
        return result.ExitCode == 0;
    }

}
