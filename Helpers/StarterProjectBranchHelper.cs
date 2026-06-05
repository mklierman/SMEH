using Spectre.Console;

namespace SMEH.Helpers;

/// <summary>Prompts for SatisfactoryModLoader clone branch (master or dev).</summary>
public static class StarterProjectBranchHelper
{
    public const string Master = AppDefaults.StarterProjectBranch;
    public const string Dev = AppDefaults.StarterProjectDevBranch;

    public static void Prompt(StarterProjectOptions options)
    {
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("SatisfactoryModLoader branch:")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices(
                $"{Master} (stable)",
                $"{Dev} (latest development)"));
        options.Branch = choice.StartsWith(Dev, StringComparison.OrdinalIgnoreCase) ? Dev : Master;
    }
}
