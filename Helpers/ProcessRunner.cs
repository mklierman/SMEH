using System.Diagnostics;
using System.IO;
using System.Text;
using Spectre.Console;

namespace SMEH.Helpers;

public class ProcessRunner
{
    /// <summary>Runs a process and returns the result.</summary>
    /// <param name="fileName">The name of the executable to run.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="waitForExit">Whether to wait for the process to exit.</param>
    /// <returns>The result of the process run.</returns>  
    public async Task<ProcessRunResult> RunAsync(string fileName, string? arguments = null, string? workingDirectory = null, bool waitForExit = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? "",
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        PrintCommand(fileName, psi.Arguments, psi.WorkingDirectory);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (waitForExit)
            await process.WaitForExitAsync();

        return new ProcessRunResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    /// <summary>Runs a process and streams stdout and stderr to the console in real time. If sendInputWhenLine is set, when a line matches it returns the string to send to stdin. If sendInputAfterDelayMs and sendInputAfterDelay are set, that string is sent to stdin after the delay. If attachStdinToConsole is true, stdin is not redirected so the child gets the real console (avoids "handle is invalid" from some tools). If heartbeatInterval and heartbeatMessage are set, prints the message periodically while waiting.</summary>
    public async Task<ProcessRunResult> RunWithConsoleOutputAsync(string fileName, string? arguments = null, string? workingDirectory = null, bool waitForExit = true, Func<string, string?>? sendInputWhenLine = null, int? sendInputAfterDelayMs = null, string? sendInputAfterDelay = null, bool attachStdinToConsole = false, TimeSpan? heartbeatInterval = null, string? heartbeatMessage = null)
    {
        var redirectStdin = !attachStdinToConsole && (sendInputWhenLine != null || (sendInputAfterDelayMs.HasValue && !string.IsNullOrEmpty(sendInputAfterDelay)));
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? "",
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectStdin,
            CreateNoWindow = false
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                AnsiConsole.WriteLine(e.Data);
                stdout.AppendLine(e.Data);
                if (redirectStdin && sendInputWhenLine?.Invoke(e.Data) is { } response)
                    TrySendInput(process, response);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                AnsiConsole.WriteLine(e.Data);
                stderr.AppendLine(e.Data);
                if (redirectStdin && sendInputWhenLine?.Invoke(e.Data) is { } response)
                    TrySendInput(process, response);
            }
        };

        PrintCommand(fileName, psi.Arguments, psi.WorkingDirectory);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!attachStdinToConsole && sendInputAfterDelayMs.HasValue && !string.IsNullOrEmpty(sendInputAfterDelay))
            _ = SendInputAfterDelayAsync(process, sendInputAfterDelayMs.Value, sendInputAfterDelay);

        if (waitForExit)
        {
            if (heartbeatInterval.HasValue && !string.IsNullOrEmpty(heartbeatMessage))
            {
                var exitTask = process.WaitForExitAsync();
                while (!exitTask.IsCompleted)
                {
                    var completed = await Task.WhenAny(exitTask, Task.Delay(heartbeatInterval.Value));
                    if (completed == exitTask)
                        break;
                    AnsiConsole.MarkupLine($"[dim]{heartbeatMessage}[/]");
                }
                await exitTask;
            }
            else
            {
                await process.WaitForExitAsync();
            }
        }

        return new ProcessRunResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }

    private static void PrintCommand(string fileName, string arguments, string workingDirectory)
    {
        var cmd = string.IsNullOrEmpty(arguments) ? fileName : $"{fileName} {arguments}";
        AnsiConsole.MarkupLineInterpolated($"[dim]> {Markup.Escape(cmd)}[/]");
        AnsiConsole.MarkupLineInterpolated($"[dim]> (in {Markup.Escape(workingDirectory)})[/]");
    }

    private static async Task SendInputAfterDelayAsync(Process process, int delayMs, string input)
    {
        try
        {
            await Task.Delay(delayMs);
            if (process.HasExited)
                return;
            try
            {
                process.StandardInput.Write(input);
                process.StandardInput.Flush();
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (InvalidOperationException) { }
            catch (Exception ex) when (ex.Message.Contains("handle", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)) { }
        }
        catch (TaskCanceledException) { }
    }

    private static void TrySendInput(Process process, string response)
    {
        if (process.HasExited)
            return;
        try
        {
            process.StandardInput.WriteLine(response);
            process.StandardInput.Flush();
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        catch (InvalidOperationException) { }
        catch (Exception ex) when (ex.Message.Contains("handle", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)) { }
    }
}

public record ProcessRunResult(int ExitCode, string StdOut, string StdError);
