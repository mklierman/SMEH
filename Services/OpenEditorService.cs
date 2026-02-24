using System.Diagnostics;
using Spectre.Console;
using SMEH;
using SMEH.Helpers;

namespace SMEH.Services;

/// <summary>Opens the project’s FactoryGame.uproject in the default application (Unreal Editor); menu option 9.</summary>
public class OpenEditorService
{
    private readonly WwiseCliOptions _wwiseCliOptions;

    public OpenEditorService(WwiseCliOptions wwiseCliOptions)
    {
        _wwiseCliOptions = wwiseCliOptions;
    }

    public Task<bool> RunAsync()
    {
        var projectDir = ProjectPathHelper.ResolveStarterProjectPath(_wwiseCliOptions);
        if (string.IsNullOrEmpty(projectDir))
            return Task.FromResult(false);

        var uprojectPath = Path.Combine(projectDir, "FactoryGame.uproject");
        if (!File.Exists(uprojectPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]FactoryGame.uproject not found at: {Markup.Escape(uprojectPath)}[/]");
            if (!ProjectPathHelper.TryPromptProjectPath(out var promptedDir, out var promptedUproject))
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

}
