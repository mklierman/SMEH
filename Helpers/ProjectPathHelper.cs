using Spectre.Console;
using SMEH;

namespace SMEH.Helpers;

/// <summary>
/// Shared logic for resolving and prompting project/engine paths used by Build Editor,
/// Generate VS Project, Wwise CLI, and Open Editor services.
/// </summary>
public static class ProjectPathHelper
{
    /// <summary>
    /// Resolves the SatisfactoryModLoader clone path from options, last clone state, or user prompt.
    /// </summary>
    public static string? ResolveStarterProjectPath(WwiseCliOptions options)
    {
        var path = options.StarterProjectPath?.Trim();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = SmehState.GetLastClonePath();
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        AnsiConsole.MarkupLine("[yellow]Starter project path not found. Run option 5 (Starter Project) first to clone the repo,[/]");
        AnsiConsole.MarkupLine("[yellow]or enter the clone directory when prompted.[/]");
        path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to SatisfactoryModLoader clone (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrEmpty(path?.Trim()))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
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

    /// <summary>
    /// Prompts for a project folder or FactoryGame.uproject path; validates and returns directory and .uproject path.
    /// </summary>
    public static bool TryPromptProjectPath(out string? projectDir, out string? uprojectPath)
    {
        projectDir = null;
        uprojectPath = null;
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to project folder or to FactoryGame.uproject (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
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

    /// <summary>
    /// Prompts for the CSS Unreal Engine installation path and assigns it to <paramref name="cssPath"/>.
    /// </summary>
    public static bool TryPromptEnginePath(ref string cssPath)
    {
        var path = AnsiConsole.Prompt(new TextPrompt<string>("Enter path to CSS Unreal Engine installation (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(path))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
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
