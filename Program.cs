using Microsoft.Extensions.Configuration;
using Spectre.Console;
using SMEH;
using SMEH.Helpers;
using SMEH.Services;

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

var options = new SmehOptions();
config.Bind(options);

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

while (true)
{
    var choice = ShowMenu();
    if (choice == null)
        continue;

    if (choice == "0")
    {
        AnsiConsole.MarkupLine("[green]Goodbye.[/]");
        return 0;
    }

    try
    {
        var completed = choice switch
        {
            "1" => await RunOptionAsync("Visual Studio 2022", () => visualStudioService.RunAsync()),
            "2" => await RunOptionAsync("Clang", () => clangService.RunAsync()),
            "3" => await RunOptionAsync("CSS Unreal Engine", () => cssUnrealService.RunAsync()),
            "4" => await RunOptionAsync("Starter Project (SML)", () => starterProjectService.RunAsync()),
            "5" => await RunOptionAsync("Wwise-CLI", () => wwiseCliService.RunAsync()),
            "6" => await RunOptionAsync("Cleanup temp files", () => cleanupService.RunAsync()),
            "7" => await RunOptionAsync("Generate Visual Studio project files", () => generateVsProjectService.RunAsync()),
            "8" => await RunOptionAsync("Build Editor", () => buildEditorService.RunAsync()),
            "9" => await RunOptionAsync("Open in Unreal Editor", () => openEditorService.RunAsync()),
            _ => false
        };

        if (!completed)
        {
            AnsiConsole.MarkupLine("[yellow]Invalid option. Please choose 1-9 or 0 to exit.[/]");
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
    AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
    Console.ReadKey(true);
    AnsiConsole.Clear();
}

static string? ShowMenu()
{
    AnsiConsole.Write(new Rule("[bold blue]SMEH Menu[/]").RuleStyle("grey"));
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select option:")
            .AddChoices(
                "1. Visual Studio 2022",
                "2. Clang",
                "3. CSS Unreal Engine",
                "4. Starter Project",
                "5. Wwise",
                "6. Cleanup temp files",
                "7. Generate Visual Studio project files",
                "8. Build Editor",
                "9. Open in Unreal Editor",
                "0. Exit"
            ));
    return choice.Length >= 1 ? choice[0].ToString() : null;
}

static async Task<bool> RunOptionAsync(string name, Func<Task> run)
{
    await run();
    return true;
}
