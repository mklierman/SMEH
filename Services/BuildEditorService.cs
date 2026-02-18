using Spectre.Console;
using SMEH;
using SMEH.Helpers;

namespace SMEH.Services;

public class BuildEditorService
{
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly WwiseCliOptions _wwiseCliOptions;
    private readonly ProcessRunner _processRunner;

    public BuildEditorService(CssUnrealEngineOptions cssUnrealEngineOptions, WwiseCliOptions wwiseCliOptions, ProcessRunner processRunner)
    {
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _wwiseCliOptions = wwiseCliOptions;
        _processRunner = processRunner;
    }

    public async Task<bool> RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return false;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (string.IsNullOrEmpty(cssPath))
            cssPath = @"C:\Program Files\Unreal Engine - CSS";
        // If a starter project path is already known (via config or a previous
        // manual entry saved in SmehState), allow building the editor without
        // forcing the SMEH "previous steps" to be marked as completed. This lets
        // users who set paths manually still use step 8.
        var hasKnownStarterProjectPath =
            !string.IsNullOrWhiteSpace(_wwiseCliOptions.StarterProjectPath) ||
            !string.IsNullOrEmpty(SmehState.GetLastClonePath());
        if (!hasKnownStarterProjectPath)
        {
            if (!SmehState.EnsureStepsCompleted(
                    new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine, SmehState.StepStarterProject },
                    projectDir,
                    cssPath))
            {
                return false;
            }
        }

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!TryPromptProjectPath(out projectDir, out uprojectPath))
                return false;
            SmehState.SetLastClonePath(projectDir!);
        }

        var batchDir = Path.Combine(cssPath, "Engine", "Build", "BatchFiles");
        var buildBat = Path.Combine(batchDir, "Build.bat");
        if (!File.Exists(buildBat))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Build.bat not found at: {Markup.Escape(buildBat)}[/]");
            if (!TryPromptEnginePath(ref cssPath))
                return false;
            batchDir = Path.Combine(cssPath, "Engine", "Build", "BatchFiles");
            buildBat = Path.Combine(batchDir, "Build.bat");
            if (!File.Exists(buildBat))
            {
                AnsiConsole.MarkupLine("[red]Build.bat still not found at that path.[/]");
                return false;
            }
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath!);
        var args = $"FactoryEditor Win64 Development -Project=\"{fullUprojectPath}\" -WaitMutex -FromMsBuild";
        AnsiConsole.MarkupLine("[dim]Building FactoryEditor (Development Editor, Win64)...[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]Project: {Markup.Escape(fullUprojectPath)}[/]");
        var result = await _processRunner.RunAsync(buildBat, args, batchDir, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Build failed (exit code {result.ExitCode}).[/]");
            if (!string.IsNullOrEmpty(result.StdError))
                AnsiConsole.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                AnsiConsole.WriteLine(result.StdOut);
            return false;
        }
        AnsiConsole.MarkupLine("[green]Build completed successfully.[/]");
        return true;
    }

    private string? ResolveStarterProjectPath()
    {
        var path = _wwiseCliOptions.StarterProjectPath?.Trim();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = SmehState.GetLastClonePath();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        AnsiConsole.MarkupLine("[yellow]Starter project path not found. Run option 4 (Starter Project) first to clone the repo,[/]");
        AnsiConsole.MarkupLine("[yellow]or enter the clone directory when prompted.[/]");
        path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to SatisfactoryModLoader clone (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrEmpty(path?.Trim()))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return null;
        }
        path = path.Trim();
        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Directory not found: {Markup.Escape(path)}[/]");
            return null;
        }
        SmehState.SetLastClonePath(path);
        return path;
    }

    private static bool TryPromptProjectPath(out string? projectDir, out string? uprojectPath)
    {
        projectDir = null;
        uprojectPath = null;
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to project folder or to FactoryGame.uproject (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return false;
        }
        path = path!.Trim();
        if (File.Exists(path) && path.EndsWith("FactoryGame.uproject", StringComparison.OrdinalIgnoreCase))
        {
            projectDir = Path.GetDirectoryName(path);
            uprojectPath = path;
            return !string.IsNullOrEmpty(projectDir);
        }
        if (Directory.Exists(path))
        {
            uprojectPath = Path.Combine(path, "FactoryGame.uproject");
            if (File.Exists(uprojectPath))
            {
                projectDir = path;
                return true;
            }
        }
        AnsiConsole.MarkupLine("[red]FactoryGame.uproject not found at that path.[/]");
        return false;
    }

    private static bool TryPromptEnginePath(ref string cssPath)
    {
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to CSS Unreal Engine installation (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return false;
        }
        path = path!.Trim();
        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Directory not found: {Markup.Escape(path)}[/]");
            return false;
        }
        cssPath = path;
        return true;
    }
}
