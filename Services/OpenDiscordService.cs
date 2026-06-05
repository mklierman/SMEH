using System.Diagnostics;
using Spectre.Console;

namespace SMEH.Services;

/// <summary>Opens the modding Discord in the default browser.</summary>
public class OpenDiscordService
{
    private const string DiscordUrl = "https://discord.ficsit.app/";

    public Task<bool> RunAsync()
    {
        AnsiConsole.MarkupLineInterpolated($"[dim]Opening Discord: {Markup.Escape(DiscordUrl)}[/]");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = DiscordUrl,
                UseShellExecute = true
            });

            if (process != null)
            {
                AnsiConsole.MarkupLine("[green]Modding Discord is opening in your default browser.[/]");
                return Task.FromResult(true);
            }

            AnsiConsole.MarkupLine("[red]Failed to open Modding Discord.[/]");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Failed to open Modding Discord: {Markup.Escape(ex.Message)}[/]");
            return Task.FromResult(false);
        }
    }
}
