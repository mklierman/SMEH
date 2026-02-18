using Spectre.Console;
using SMEH;

namespace SMEH.Services;

public class CleanupService
{
    /// <summary>Root temp directory used by the app (VS2022, Clang, CssUnrealEngine, WwiseCLI).</summary>
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "SMEH");

    public Task<bool> RunAsync()
    {
        var path = TempRoot;
        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLine("[dim]No SMEH temp files found. Nothing to clean.[/]");
            return Task.FromResult(true); // Nothing to do, consider it success
        }

        AnsiConsole.MarkupLine("The following directory and all its contents will be deleted:");
        AnsiConsole.MarkupLineInterpolated($"[yellow]  {Markup.Escape(path)}[/]");
        AnsiConsole.WriteLine();
        var answer = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Are you sure?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (answer != "Yes")
        {
            AnsiConsole.MarkupLine("[dim]Cleanup cancelled.[/]");
            return Task.FromResult(false);
        }

        try
        {
            Directory.Delete(path, recursive: true);
            AnsiConsole.MarkupLine("[green]Temp files deleted successfully.[/]");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Could not delete all temp files: {Markup.Escape(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[yellow]Some files may be in use. Close other programs or try again later.[/]");
            return Task.FromResult(false);
        }
    }
}
