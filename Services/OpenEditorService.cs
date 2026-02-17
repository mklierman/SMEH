using System.Diagnostics;
using Spectre.Console;
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

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            AnsiConsole.MarkupLine("[yellow]Ensure the Starter Project (option 4) is cloned and contains FactoryGame.uproject.[/]");
            return Task.CompletedTask;
        }

        var fullUprojectPath = Path.GetFullPath(uprojectPath);
        AnsiConsole.MarkupLineInterpolated($"[dim]Opening project: {Markup.Escape(fullUprojectPath)}[/]");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fullUprojectPath,
                UseShellExecute = true
            });
            if (process != null)
                AnsiConsole.MarkupLine("[green]Project is opening. You can return to the menu.[/]");
            else
                AnsiConsole.MarkupLine("[red]Failed to open project.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to open project: {Markup.Escape(ex.Message)}[/]");
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
