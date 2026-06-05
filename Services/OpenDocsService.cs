using System.Diagnostics;
using Spectre.Console;

namespace SMEH.Services;

/// <summary>Opens the FICSIT documentation in the default browser.</summary>
public class OpenDocsService
{
    private const string DocsUrl = "https://docs.ficsit.app/";

    public Task<bool> RunAsync(string url = DocsUrl, string label = "docs")
    {
        AnsiConsole.MarkupLineInterpolated($"[dim]Opening {Markup.Escape(label)}: {Markup.Escape(url)}[/]");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            if (process != null)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]{Markup.Escape(label)} are opening in your default browser.[/]");
                return Task.FromResult(true);
            }

            AnsiConsole.MarkupLineInterpolated($"[red]Failed to open {Markup.Escape(label)}.[/]");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to open {Markup.Escape(label)}: {Markup.Escape(ex.Message)}[/]");
            return Task.FromResult(false);
        }
    }
}
