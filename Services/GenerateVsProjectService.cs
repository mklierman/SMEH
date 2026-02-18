using Spectre.Console;
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

    public async Task<bool> RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return false;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (string.IsNullOrEmpty(cssPath))
            cssPath = @"C:\Program Files\Unreal Engine - CSS";
        // For generating Visual Studio project files, we only require
        // Visual Studio, Clang, and CSS Unreal Engine. If the user has
        // provided a valid starter project path (either via config or
        // this prompt), we do NOT force step 4 (Starter Project) to have
        // been run via SMEH itself.
        if (!SmehState.EnsureStepsCompleted(
                new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine },
                projectDir,
                cssPath))
        {
            return false;
        }

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!TryPromptProjectPath(out projectDir, out uprojectPath))
                return false;
            SmehState.SetLastClonePath(projectDir!);
        }

        var unrealBuildToolPath = Path.Combine(cssPath, "Engine", "Binaries", "DotNET", "UnrealBuildTool", "UnrealBuildTool.exe");
        if (!File.Exists(unrealBuildToolPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]UnrealBuildTool.exe not found at: {Markup.Escape(unrealBuildToolPath)}[/]");
            if (!TryPromptEnginePath(ref cssPath))
                return false;
            unrealBuildToolPath = Path.Combine(cssPath, "Engine", "Binaries", "DotNET", "UnrealBuildTool", "UnrealBuildTool.exe");
            if (!File.Exists(unrealBuildToolPath))
            {
                AnsiConsole.MarkupLine("[red]UnrealBuildTool.exe still not found at that path.[/]");
                return false;
            }
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath!);
        var args = $"-projectfiles -project=\"{fullUprojectPath}\" -game -rocket -progress";
        AnsiConsole.MarkupLineInterpolated($"[dim]Generating Visual Studio project files for {Markup.Escape(fullUprojectPath)}...[/]");
        var result = await _processRunner.RunAsync(unrealBuildToolPath, args, cssPath, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]UnrealBuildTool failed (exit code {result.ExitCode}).[/]");
            if (!string.IsNullOrEmpty(result.StdError))
                AnsiConsole.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                AnsiConsole.WriteLine(result.StdOut);
            return false;
        }
        AnsiConsole.MarkupLine("[green]Visual Studio project files generated successfully.[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]Solution and projects are in: {Markup.Escape(projectDir!)}[/]");
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
