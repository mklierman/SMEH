using Spectre.Console;
using SMEH;
using SMEH.Helpers;

namespace SMEH.Services;

/// <summary>Generates Visual Studio solution and project files for the starter project via UnrealBuildTool</summary>
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
        var projectDir = ProjectPathHelper.ResolveStarterProjectPath(_wwiseCliOptions);
        if (string.IsNullOrEmpty(projectDir))
        {
            return false;
        }

        var cssPath = _cssUnrealEngineOptions.EffectiveInstallPath;
        if (!SmehState.EnsureStepsCompleted(
                new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine },
                projectDir,
                cssPath))
        {
            return false;
        }

        var uprojectPath = Path.Combine(projectDir, AppDefaults.StarterProjectFileName);
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!ProjectPathHelper.TryPromptProjectPath(out projectDir, out uprojectPath))
            {
                return false;
            }

            SmehState.SetLastClonePath(projectDir!);
        }

        var unrealBuildToolPath = Path.Combine(cssPath, "Engine", "Binaries", "DotNET", "UnrealBuildTool", "UnrealBuildTool.exe");
        if (!File.Exists(unrealBuildToolPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]UnrealBuildTool.exe not found at: {Markup.Escape(unrealBuildToolPath)}[/]");
            if (!ProjectPathHelper.TryPromptEnginePath(ref cssPath))
            {
                return false;
            }

            unrealBuildToolPath = Path.Combine(cssPath, "Engine", "Binaries", "DotNET", "UnrealBuildTool", "UnrealBuildTool.exe");
            if (!File.Exists(unrealBuildToolPath))
            {
                AnsiConsole.MarkupLine("[red]UnrealBuildTool.exe still not found at that path.[/]");
                return false;
            }
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath!);
        var args = $"-projectfiles -project=\"{fullUprojectPath}\" -game -rocket -progress";
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Generating Visual Studio project files...[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]{Markup.Escape(fullUprojectPath)}[/]");
        var result = await _processRunner.RunWithConsoleOutputAsync(unrealBuildToolPath, args, cssPath, waitForExit: true);
        if (result.ExitCode != 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]UnrealBuildTool failed (exit code {result.ExitCode}).[/]");
            if (!string.IsNullOrEmpty(result.StdError))
            {
                AnsiConsole.WriteLine(result.StdError);
            }

            if (!string.IsNullOrEmpty(result.StdOut))
            {
                AnsiConsole.WriteLine(result.StdOut);
            }

            return false;
        }
        AnsiConsole.MarkupLine("[green]Visual Studio project files generated successfully.[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]Solution and projects are in: {Markup.Escape(projectDir!)}[/]");
        return true;
    }
}
