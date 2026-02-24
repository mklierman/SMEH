using System.Text.RegularExpressions;
using Spectre.Console;
using SMEH;
using SMEH.Helpers;

namespace SMEH.Services;

/// <summary>Builds the FactoryEditor (Development Editor, Win64) via the CSS Unreal Engine Build.bat; menu option 8.</summary>
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
        var projectDir = ProjectPathHelper.ResolveStarterProjectPath(_wwiseCliOptions);
        if (string.IsNullOrEmpty(projectDir))
            return false;

        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (string.IsNullOrEmpty(cssPath))
            cssPath = AppDefaults.CssUnrealEngineInstallPath;
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
            if (!ProjectPathHelper.TryPromptProjectPath(out projectDir, out uprojectPath))
                return false;
            SmehState.SetLastClonePath(projectDir!);
        }

        var batchDir = Path.Combine(cssPath, "Engine", "Build", "BatchFiles");
        var buildBat = Path.Combine(batchDir, "Build.bat");
        if (!File.Exists(buildBat))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Build.bat not found at: {Markup.Escape(buildBat)}[/]");
            if (!ProjectPathHelper.TryPromptEnginePath(ref cssPath))
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
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Building FactoryEditor (Development Editor, Win64)...[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]Project: {Markup.Escape(fullUprojectPath)}[/]");
        var result = await _processRunner.RunWithProgressAsync(buildBat, args, batchDir, waitForExit: true, progressParser: ParseBuildProgress, progressTaskName: "FactoryEditor");
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

    /// <summary>Parses Unreal Build Tool progress from lines like "Building 319 action(s) started" and "[5/319] Compile ...".</summary>
    private static (int current, int total)? ParseBuildProgress(string line)
    {
        var buildingMatch = Regex.Match(line, @"Building\s+(\d+)\s+action");
        if (buildingMatch.Success)
            return (0, int.Parse(buildingMatch.Groups[1].Value));
        var stepMatch = Regex.Match(line, @"\[(\d+)/(\d+)\]");
        if (stepMatch.Success)
            return (int.Parse(stepMatch.Groups[1].Value), int.Parse(stepMatch.Groups[2].Value));
        return null;
    }
}
