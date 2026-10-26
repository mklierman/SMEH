using Spectre.Console;

namespace SMEH.Helpers;

/// <summary>
/// Shows a status spinner while long-running, silent work (e.g. an installer) runs.
/// Spectre allows only one live display at a time, so callers must not already be inside a spinner or progress display.
/// </summary>
public static class StatusSpinner
{
    public static Task<T> RunAsync<T>(string statusMessage, Func<Task<T>> run)
    {
        return AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(statusMessage, _ => run());
    }
}
