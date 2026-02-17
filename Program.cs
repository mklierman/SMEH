using Microsoft.Extensions.Configuration;
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
var starterProjectService = new StarterProjectService(options.StarterProject, options.CssUnrealEngine, processRunner);
var cleanupService = new CleanupService();

while (true)
{
    ShowMenu();
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        continue;

    if (input == "0" || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Goodbye.");
        return 0;
    }

    try
    {
        var completed = input switch
        {
            "1" => await RunOptionAsync("Visual Studio 2022", () => visualStudioService.RunAsync()),
            "2" => await RunOptionAsync("Clang", () => clangService.RunAsync()),
            "3" => await RunOptionAsync("CSS Unreal Engine", () => cssUnrealService.RunAsync()),
            "4" => await RunOptionAsync("Starter Project (SML)", () => starterProjectService.RunAsync()),
            "5" => await RunOptionAsync("Wwise-CLI", () => wwiseCliService.RunAsync()),
            "6" => await RunOptionAsync("Cleanup temp files", () => cleanupService.RunAsync()),
            "7" => await RunOptionAsync("Clear GitHub token", () =>
            {
                SmehState.SetGitHubAccessToken(null);
                Console.WriteLine("GitHub token cleared. You will be asked to authorize again when needed (e.g. option 3).");
                return Task.CompletedTask;
            }),
            _ => false
        };

        if (!completed)
        {
            Console.WriteLine("Invalid option. Please choose 1-7 or 0 to exit.");
        }
    }
    catch (InvalidProgramException ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine("This can sometimes be fixed by rebuilding the application (dotnet build) or using a different .NET runtime.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }

    Console.WriteLine();
    Console.WriteLine("Press any key to return to menu...");
    Console.ReadKey(true);
    Console.Clear();
}

static void ShowMenu()
{
    Console.WriteLine("=== SMEH Menu ===");
    Console.WriteLine("1. Visual Studio 2022");
    Console.WriteLine("2. Clang");
    Console.WriteLine("3. CSS Unreal Engine");
    Console.WriteLine("4. Starter Project");
    Console.WriteLine("5. Wwise");
    Console.WriteLine("6. Cleanup temp files");
    Console.WriteLine("7. Clear GitHub token");
    Console.WriteLine("0. Exit");
    Console.Write("Select option (0-7): ");
}

static async Task<bool> RunOptionAsync(string name, Func<Task> run)
{
    await run();
    return true;
}
