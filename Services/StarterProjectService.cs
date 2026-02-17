using System.Diagnostics;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

public class StarterProjectService
{
    private readonly StarterProjectOptions _options;
    private readonly CssUnrealEngineOptions _cssUnrealEngineOptions;
    private readonly ProcessRunner _processRunner;

    public StarterProjectService(StarterProjectOptions options, CssUnrealEngineOptions cssUnrealEngineOptions, ProcessRunner processRunner)
    {
        _options = options;
        _cssUnrealEngineOptions = cssUnrealEngineOptions;
        _processRunner = processRunner;
    }

    public async Task RunAsync()
    {
        var cssPath = _cssUnrealEngineOptions.InstallPath?.Trim();
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang, SmehState.StepCssUnrealEngine }, cssUnrealEnginePath: string.IsNullOrEmpty(cssPath) ? null : cssPath))
            return;

        Console.Write("Enter install location for the starter project (e.g. C:\\Modding): ");
        var basePath = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(basePath))
        {
            Console.WriteLine("No path entered. Aborted.");
            return;
        }
        if (!Directory.Exists(basePath))
        {
            try
            {
                Directory.CreateDirectory(basePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not create directory: {ex.Message}");
                return;
            }
        }
        var targetPath = Path.Combine(basePath, "SatisfactoryModLoader");

        if (Directory.Exists(targetPath))
        {
            var hasGit = Directory.Exists(Path.Combine(targetPath, ".git"));
            if (hasGit)
            {
                Console.WriteLine($"Directory already exists and appears to be a git clone: {targetPath}");
                Console.WriteLine("Choose a different path or remove the existing folder.");
                return;
            }
        }

        // Ensure git is on PATH
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            p?.WaitForExit(5000);
            if (p == null || p.ExitCode != 0)
                throw new InvalidOperationException("git not found");
        }
        catch
        {
            Console.WriteLine("Git was not found on PATH. Please install Git and ensure it is in your PATH.");
            return;
        }

        Console.WriteLine($"Cloning {_options.RepositoryUrl} (branch: {_options.Branch}) to {targetPath}...");
        var args = $"clone --branch \"{_options.Branch}\" \"{_options.RepositoryUrl}\" \"{targetPath}\"";
        var result = await _processRunner.RunAsync("git", args, null, waitForExit: true);

        if (result.ExitCode != 0)
        {
            Console.WriteLine("Clone failed.");
            if (!string.IsNullOrEmpty(result.StdError))
                Console.WriteLine(result.StdError);
            if (!string.IsNullOrEmpty(result.StdOut))
                Console.WriteLine(result.StdOut);
            return;
        }

        SmehState.SetLastClonePath(targetPath);
        Console.WriteLine($"Successfully cloned to {targetPath}");
    }
}
