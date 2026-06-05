using Spectre.Console;

namespace SMEH.Helpers;

/// <summary>Prompts for and supplies Wwise / Audiokinetic credentials used by wwise-cli.</summary>
public static class WwiseCredentialsHelper
{
    public static bool Ensure(WwiseCliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Email) && !string.IsNullOrWhiteSpace(options.Password))
        {
            return true;
        }

        AnsiConsole.MarkupLine("[dim]Wwise SDK download requires an Audiokinetic account (free at audiokinetic.com).[/]");
        options.Email = AnsiConsole.Prompt(new TextPrompt<string>("Wwise / Audiokinetic email:")
            .ValidationErrorMessage("[red]Email is required[/]")
            .Validate(e => !string.IsNullOrWhiteSpace(e)))
            .Trim();
        options.Password = AnsiConsole.Prompt(new TextPrompt<string>("Wwise / Audiokinetic password:")
            .Secret()
            .ValidationErrorMessage("[red]Password is required[/]")
            .Validate(p => !string.IsNullOrWhiteSpace(p)));
        return true;
    }

    public static string QuoteForCli(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
