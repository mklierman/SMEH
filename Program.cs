using System.Reflection;
using Spectre.Console;
using SMEH;
using SMEH.Helpers;
using SMEH.Services;

var options = AppDefaults.CreateOptions();

var downloadHelper = new DownloadHelper();
var processRunner = new ProcessRunner();

var visualStudioService = new VisualStudioInstallerService(options.VisualStudio, downloadHelper, processRunner);
var clangService = new ClangInstallerService(options.Clang, downloadHelper, processRunner);
var cssUnrealService = new CssUnrealEngineService(options.CssUnrealEngine, downloadHelper, processRunner);
var wwiseCliService = new WwiseCliService(options.WwiseCli, options.CssUnrealEngine, downloadHelper, processRunner);
var starterProjectService = new StarterProjectService(options.StarterProject, options.CssUnrealEngine, downloadHelper, processRunner);
var generateVsProjectService = new GenerateVsProjectService(options.CssUnrealEngine, options.WwiseCli, processRunner);
var buildEditorService = new BuildEditorService(options.CssUnrealEngine, options.WwiseCli, processRunner);
var openEditorService = new OpenEditorService(options.CssUnrealEngine, options.WwiseCli);
var cleanupService = new CleanupService();

var version = Assembly.GetExecutingAssembly().GetName().Version;
var versionString = version != null ? $"{version.Major}.{version.Minor}" : "1.0";

while (true)
{
    AnsiConsole.Clear();
    AnsiConsole.Write(
        new Panel(new Markup($"[bold {SmehTheme.AccentHex}]SMEH[/]\n[{SmehTheme.TextSecondaryHex}]Satisfactory Modding Environment Helper[/]  [{SmehTheme.TextSecondaryHex}]v{Markup.Escape(versionString)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(SmehTheme.Accent)
            .Padding(1, 0));
    AnsiConsole.WriteLine();

    var choice = ShowMenu(options);
    if (choice == null)
        continue;

    if (choice == "0")
    {
        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"[{SmehTheme.TextSecondaryHex}]Exit SMEH?[/]")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (confirm != "Yes")
            continue;
        AnsiConsole.Write(new Panel("[green]✓ Goodbye.[/]").Border(BoxBorder.Rounded).BorderColor(SmehTheme.Accent).Padding(0, 0));
        return 0;
    }

    try
    {
        var completed = choice switch
        {
            "1" => await RunOptionAsync("Setting up CSS Unreal Engine", () => cssUnrealService.RunAsync()),
            "2" => await RunOptionAsync("Installing Visual Studio 2022...", () => visualStudioService.RunAsync()),
            "3" => await RunOptionAsync("Installing Clang...", () => clangService.RunAsync()),
            "4" => await RunOptionAsync("Cloning Starter Project", () => starterProjectService.RunAsync()),
            "5" => await RunOptionAsync("Setting up Wwise-CLI", () => wwiseCliService.RunAsync()),
            "6" => await RunOptionAsync("Generating Visual Studio project files...", () => generateVsProjectService.RunAsync()),
            "7" => await RunOptionAsync("Building Editor...", () => buildEditorService.RunAsync()),
            "8" => await RunOptionAsync("Opening project", () => openEditorService.RunAsync()),
            "9" => await RunOptionAsync("Cleanup temp files", () => cleanupService.RunAsync()),
            _ => false
        };

        if (!completed)
        {
            // Only show "Invalid option" when the user's choice was not 1-9 (e.g. wrong key). When a valid option failed (e.g. folder not found), the service already showed the reason.
            if (choice is not "1" and not "2" and not "3" and not "4" and not "5" and not "6" and not "7" and not "8" and not "9")
                AnsiConsole.MarkupLineInterpolated($"[{SmehTheme.AccentHex}]Invalid option. Please choose 1-9 or 0 to exit.[/]");
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel("[green]✓ Operation completed successfully.[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(SmehTheme.Accent)
                .Padding(1, 0));
        }
    }
    catch (InvalidProgramException ex)
    {
        AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {Markup.Escape(ex.Message)}");
        AnsiConsole.MarkupLine("This can sometimes be fixed by rebuilding the application (dotnet build) or using a different .NET runtime.");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {Markup.Escape(ex.Message)}");
    }

    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Rule($"[{SmehTheme.TextSecondaryHex}]Press any key to return to menu[/]").RuleStyle(SmehTheme.Border));
    Console.ReadKey(true);
}

static string? ShowMenu(SmehOptions options)
{
    AnsiConsole.MarkupLineInterpolated($"[{SmehTheme.TextSecondaryHex}]Use Up/Down and Enter to select, or type to search.[/]");

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title($"[{SmehTheme.AccentHex}]Select option:[/]")
            .HighlightStyle(SmehTheme.AccentStyle)
            .EnableSearch()
            .SearchPlaceholderText($"[{SmehTheme.TextSecondaryHex}]Type to filter (e.g. build, wwise, exit)...[/]")
            .AddChoices(
                "1. CSS Unreal Engine",
                "2. Visual Studio 2022",
                "3. Clang",
                "4. Starter Project",
                "5. Wwise",
                "6. Generate Visual Studio project files",
                "7. Build Editor",
                "8. Open in Unreal Editor",
                "9. Cleanup temp files",
                "0. Exit"
            ));
    return choice.Length >= 1 ? choice[0].ToString() : null;
}

static async Task<bool> RunOptionAsync(string statusMessage, Func<Task<bool>> run)
{
    var useSpinner = statusMessage.EndsWith("...", StringComparison.Ordinal);
    if (useSpinner)
    {
        var completed = false;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(statusMessage, async _ => completed = await run());
        return completed;
    }
    return await run();
}
