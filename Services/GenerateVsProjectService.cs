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

    public async Task RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return;

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
            return;
        }

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            AnsiConsole.MarkupLine("[yellow]Ensure the Starter Project (option 4) is cloned and contains FactoryGame.uproject.[/]");
            return;
        }

        var unrealBuildToolPath = Path.Combine(cssPath, "Engine", "Binaries", "DotNET", "UnrealBuildTool", "UnrealBuildTool.exe");
        if (!File.Exists(unrealBuildToolPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]UnrealBuildTool.exe not found at: {Markup.Escape(unrealBuildToolPath)}[/]");
            AnsiConsole.MarkupLine("[yellow]Ensure CSS Unreal Engine is installed and CssUnrealEngine:InstallPath in appsettings.json is correct.[/]");
            return;
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath);
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
            return;
        }
        AnsiConsole.MarkupLine("[green]Visual Studio project files generated successfully.[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]Solution and projects are in: {Markup.Escape(projectDir)}[/]");
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
        AnsiConsole.MarkupLine("[yellow]or set WwiseCli:StarterProjectPath in appsettings.json to the clone directory.[/]");
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
}
