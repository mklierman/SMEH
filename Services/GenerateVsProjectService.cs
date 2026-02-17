using SMEH;
using SMEH.Helpers;

namespace SMEH.Services;

public class GenerateVsProjectService
{
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly WwiseCliOptions _wwiseCliOptions;
    private readonly ProcessRunner _processRunner;

    public GenerateVsProjectService(CssUnrealEngineOptions cssUnrealEngineOptions, WwiseCliOptions wwiseCliOptions, ProcessRunner processRunner)
    {
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _wwiseCliOptions = wwiseCliOptions;
        _processRunner = processRunner;
    }

    public async Task RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (string.IsNullOrEmpty(cssPath))
            cssPath = @"C:\Program Files\Unreal Engine - CSS";
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine, SmehState.StepStarterProject }, projectDir, cssPath))
            return;

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            Console.WriteLine($"FactoryGame.uproject not found at: {uprojectPath}");
            Console.WriteLine("Ensure the Starter Project (option 4) is cloned and contains FactoryGame.uproject.");
            return;
        }

        var batchDir = Path.Combine(cssPath, "Engine", "Build", "BatchFiles");
        var batchPath = Path.Combine(batchDir, "GenerateProjectFiles.bat");
        if (!File.Exists(batchPath))
        {
            Console.WriteLine($"GenerateProjectFiles.bat not found at: {batchPath}");
            Console.WriteLine("Ensure CSS Unreal Engine is installed and CssUnrealEngine:InstallPath in appsettings.json is correct.");
            return;
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath);
        var args = $"\"{fullUprojectPath}\" -Game -2022";
        Console.WriteLine($"Generating Visual Studio 2022 project files for {fullUprojectPath}...");
        var result = await _processRunner.RunAsync(batchPath, args, batchDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"GenerateProjectFiles failed (exit code {result.ExitCode}).");
            if (!string.IsNullOrEmpty(result.StdError))
                Console.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                Console.WriteLine(result.StdOut);
            return;
        }
        Console.WriteLine("Visual Studio project files generated successfully.");
        Console.WriteLine($"Solution and projects are in: {projectDir}");
    }

    private string? ResolveStarterProjectPath()
    {
        var path = _wwiseCliOptions.StarterProjectPath?.Trim();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = SmehState.GetLastClonePath();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        Console.WriteLine("Starter project path not found. Run option 4 (Starter Project) first to clone the repo,");
        Console.WriteLine("or set WwiseCli:StarterProjectPath in appsettings.json to the clone directory.");
        Console.Write("Enter path to SatisfactoryModLoader clone now (or press Enter to cancel): ");
        path = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine("Cancelled.");
            return null;
        }
        if (!Directory.Exists(path))
        {
            Console.WriteLine($"Directory not found: {path}");
            return null;
        }
        return path;
    }
}
