using System.Diagnostics;
using System.Text;

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

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (waitForExit)
            await process.WaitForExitAsync();

        return new ProcessRunResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd());
    }
}

public record ProcessRunResult(int ExitCode, string StdOut, string StdError);
