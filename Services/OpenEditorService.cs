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

    public Task<bool> RunAsync()
    {
        var projectDir = ResolveStarterProjectPath();
        if (string.IsNullOrEmpty(projectDir))
            return Task.FromResult(false);

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!TryPromptProjectPath(out var promptedDir, out var promptedUproject))
            {
                return Task.FromResult(false);
            }
            projectDir = promptedDir!;
            uprojectPath = promptedUproject!;
            SmehState.SetLastClonePath(projectDir);
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
            {
                AnsiConsole.MarkupLine("[green]Project is opening. You can return to the menu.[/]");
                return Task.FromResult(true);
            }
            AnsiConsole.MarkupLine("[red]Failed to open project.[/]");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to open project: {Markup.Escape(ex.Message)}[/]");
            return Task.FromResult(false);
        }
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
}
