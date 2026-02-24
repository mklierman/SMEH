using System.Diagnostics;
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
            "1" => await RunAllAsync(options, cssUnrealService, visualStudioService, clangService, starterProjectService, wwiseCliService, generateVsProjectService, buildEditorService, cleanupService),
            "2" => await RunOptionAsync("Setting up CSS Unreal Engine", () => cssUnrealService.RunAsync()),
            "3" => await RunOptionAsync("Installing Visual Studio 2022...", () => visualStudioService.RunAsync()),
            "4" => await RunOptionAsync("Installing Clang...", () => clangService.RunAsync()),
            "5" => await RunOptionAsync("Cloning Starter Project", () => starterProjectService.RunAsync()),
            "6" => await RunOptionAsync("Setting up Wwise-CLI", () => wwiseCliService.RunAsync()),
            "7" => await RunOptionAsync("Generating Visual Studio project files...", () => generateVsProjectService.RunAsync()),
            "8" => await RunOptionAsync("Building Editor", () => buildEditorService.RunAsync(), useDynamicDisplay: true),
            "9" => await RunOptionAsync("Opening project", () => openEditorService.RunAsync()),
            "10" => await RunOptionAsync("Cleanup temp files", () => cleanupService.RunAsync()),
            _ => false
        };

        if (!completed)
        {
            // Only show "Invalid option" when the user's choice was not 1-10 (e.g. wrong key). When a valid option failed (e.g. folder not found), the service already showed the reason.
            if (choice is not "1" and not "2" and not "3" and not "4" and not "5" and not "6" and not "7" and not "8" and not "9" and not "10")
                AnsiConsole.MarkupLineInterpolated($"[{SmehTheme.AccentHex}]Invalid option. Please choose 1-10 or 0 to exit.[/]");
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
                "1. Run all (unattended)",
                "2. CSS Unreal Engine",
                "3. Visual Studio 2022",
                "4. Clang",
                "5. Starter Project",
                "6. Wwise",
                "7. Generate Visual Studio project files",
                "8. Build Editor",
                "9. Open in Unreal Editor",
                "10. Cleanup temp files",
                "0. Exit"
            ));
    return choice.Length >= 1 ? choice[0].ToString() : null;
}

static async Task<bool> RunAllAsync(SmehOptions options,
    CssUnrealEngineService cssUnrealService,
    VisualStudioInstallerService visualStudioService,
    ClangInstallerService clangService,
    StarterProjectService starterProjectService,
    WwiseCliService wwiseCliService,
    GenerateVsProjectService generateVsProjectService,
    BuildEditorService buildEditorService,
    CleanupService cleanupService)
{
    AnsiConsole.MarkupLine("[bold]Run all (unattended)[/] — all input is collected now; then steps 1–7 run in order without further prompts.");
    AnsiConsole.WriteLine();

    // 1) Engine install path
    const string defaultEnginePath = @"C:\Program Files\Unreal Engine - CSS";
    AnsiConsole.MarkupLineInterpolated($"[dim]Default engine location: [white]{Markup.Escape(defaultEnginePath)}[/][/]");
    var engineChoice = AnsiConsole.Prompt(new SelectionPrompt<string>()
        .Title("Use this location or choose a custom path?")
        .HighlightStyle(SmehTheme.AccentStyle)
        .AddChoices("Use default", "Custom path"));
    if (engineChoice == "Use default")
        options.CssUnrealEngine.InstallPath = defaultEnginePath;
    else
    {
        var customEngine = AnsiConsole.Prompt(new TextPrompt<string>("Enter custom Unreal Engine install path (or press Enter to cancel):").AllowEmpty());
        if (string.IsNullOrWhiteSpace(customEngine))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return false;
        }
        options.CssUnrealEngine.InstallPath = customEngine.Trim();
    }

    // 2) Starter project base path (e.g. C:\Modding → clone will be C:\Modding\SatisfactoryModLoader)
    var starterBase = AnsiConsole.Prompt(new TextPrompt<string>("Enter folder for starter project (e.g. C:\\Modding). SatisfactoryModLoader will be cloned here:")
        .AllowEmpty());
    if (string.IsNullOrWhiteSpace(starterBase?.Trim()))
    {
        AnsiConsole.MarkupLine("[red]No path entered. Aborted.[/]");
        return false;
    }
    starterBase = starterBase!.Trim();
    options.StarterProject.DefaultClonePath = starterBase;
    options.WwiseCli.StarterProjectPath = Path.Combine(starterBase, "SatisfactoryModLoader");

    AnsiConsole.WriteLine();

    SmehState.RunAllUnattended = true;
    try
    {
        var steps = new (string Name, Func<Task<bool>> Run)[]
        {
            ("1. CSS Unreal Engine", () => cssUnrealService.RunAsync()),
            ("2. Visual Studio 2022", () => visualStudioService.RunAsync()),
            ("3. Clang", () => clangService.RunAsync()),
            ("4. Starter Project", () => starterProjectService.RunAsync()),
            ("5. Wwise-CLI", () => wwiseCliService.RunAsync()),
            ("6. Generate project files", () => generateVsProjectService.RunAsync()),
            ("7. Build Editor", () => buildEditorService.RunAsync())
        };
        var totalSw = Stopwatch.StartNew();
        for (var i = 0; i < steps.Length; i++)
        {
            var (name, run) = (steps[i].Name, steps[i].Run);
            AnsiConsole.MarkupLineInterpolated($"[bold]Step {i + 1}/7: {Markup.Escape(name)}[/]");
            var stepSw = Stopwatch.StartNew();
            var ok = await run();
            stepSw.Stop();
            if (!ok)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Run all stopped: step {i + 1} ({Markup.Escape(steps[i].Name)}) failed.[/]");
                return false;
            }
            AnsiConsole.MarkupLineInterpolated($"[dim]  Completed in {FormatDuration(stepSw.Elapsed)}.[/]");
            AnsiConsole.WriteLine();
        }
        totalSw.Stop();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup($"[green]Run all completed successfully.[/]\n[dim]Total time: {FormatDuration(totalSw.Elapsed)}[/]"))
            .Border(BoxBorder.Rounded)
            .BorderColor(SmehTheme.Accent)
            .Padding(1, 0));
        AnsiConsole.WriteLine();

        // Automatically clean up temp files.
        AnsiConsole.MarkupLine("[dim]Cleaning up temp files...[/]");
        await cleanupService.RunAsync(skipConfirmation: true);
        AnsiConsole.WriteLine();

        // Offer to delete Unreal Engine installer files (e.g. from Manual install folder).
        CssUnrealEngineService.OfferToDeleteEngineInstallerFiles();
        return true;
    }
    finally
    {
        SmehState.RunAllUnattended = false;
    }
}

static async Task<bool> RunOptionAsync(string statusMessage, Func<Task<bool>> run, bool useDynamicDisplay = false)
{
    var sw = Stopwatch.StartNew();
    bool completed;
    if (useDynamicDisplay)
        completed = await run();
    else
    {
        var useSpinner = statusMessage.EndsWith("...", StringComparison.Ordinal);
        if (useSpinner)
        {
            completed = false;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync(statusMessage, async _ => completed = await run());
        }
        else
            completed = await run();
    }
    sw.Stop();
    AnsiConsole.MarkupLineInterpolated($"[dim]Step took {FormatDuration(sw.Elapsed)}.[/]");
    return completed;
}

static string FormatDuration(TimeSpan elapsed)
{
    if (elapsed.TotalHours >= 1)
        return $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
    return $"{elapsed.Minutes}:{elapsed.Seconds:D2}";
}
