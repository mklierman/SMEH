using System.Diagnostics;
using SMEH;

namespace SMEH.Services;

public class OpenEditorService
{
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly WwiseCliOptions _wwiseCliOptions;

    public OpenEditorService(CssUnrealEngineOptions cssUnrealEngineOptions, WwiseCliOptions wwiseCliOptions)
    {
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _wwiseCliOptions = wwiseCliOptions;
    }

    public Task RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return Task.CompletedTask;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (string.IsNullOrEmpty(cssPath))
            cssPath = @"C:\Program Files\Unreal Engine - CSS";
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine, SmehState.StepStarterProject }, projectDir, cssPath))
            return Task.CompletedTask;

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            Console.WriteLine($"FactoryGame.uproject not found at: {uprojectPath}");
            Console.WriteLine("Ensure the Starter Project (option 4) is cloned and contains FactoryGame.uproject.");
            return Task.CompletedTask;
        }

        var editorExe = Path.Combine(cssPath, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
        if (!File.Exists(editorExe))
        {
            Console.WriteLine($"UnrealEditor.exe not found at: {editorExe}");
            Console.WriteLine("Ensure CSS Unreal Engine is installed and CssUnrealEngine:InstallPath in appsettings.json is correct.");
            return Task.CompletedTask;
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath);
        Console.WriteLine($"Opening Unreal Editor: {fullUprojectPath}");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = editorExe,
                Arguments = $"\"{fullUprojectPath}\"",
                WorkingDirectory = projectDir,
                UseShellExecute = true
            });
            if (process != null)
                Console.WriteLine("Unreal Editor is starting. You can return to the menu.");
            else
                Console.WriteLine("Failed to start Unreal Editor.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to launch Unreal Editor: {ex.Message}");
        }
        return Task.CompletedTask;
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
