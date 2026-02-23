using System.Diagnostics;
using Microsoft.Win32;
using Spectre.Console;

namespace SMEH;

/// <summary>Persists paths and detects installed steps (so the app can be closed and reopened between steps).</summary>
public static class SmehState
{
    /// <summary>When true, run-all flow is active; services should skip interactive prompts and use pre-set paths.</summary>
    public static bool RunAllUnattended { get; set; }

    /// <summary>Step numbers: 1=VS 2022, 2=Clang, 3=CSS Unreal Engine, 4=Starter Project, 5=Wwise.</summary>
    public const int StepVisualStudio = 1;
    public const int StepClang = 2;
    public const int StepCssUnrealEngine = 3;
    public const int StepStarterProject = 4;
    public const int StepWwise = 5;

    private static string StateDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMEH");

    private static string LastClonePathFile => Path.Combine(StateDir, "last-clone-path.txt");
    private static string GitHubTokenFile => Path.Combine(StateDir, "github-token.txt");

    public static void SetLastClonePath(string cloneDirectory)
    {
        Directory.CreateDirectory(StateDir);
        File.WriteAllText(LastClonePathFile, cloneDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    public static string? GetLastClonePath()
    {
        if (!File.Exists(LastClonePathFile))
            return null;
        var path = File.ReadAllText(LastClonePathFile).Trim();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>Returns true if the given step is detected as installed (not from stored state).</summary>
    /// <param name="cssUnrealEnginePath">Optional. If set, step 3 is checked at this path (e.g. from CssUnrealEngine:InstallPath).</param>
    public static bool IsStepCompleted(int step, string? starterProjectPathForWwise = null, string? cssUnrealEnginePath = null)
    {
        try
        {
            return step switch
            {
                StepVisualStudio => IsVisualStudio2022Installed(),
                StepClang => IsClangToolchainInstalled(),
                StepCssUnrealEngine => IsCssUnrealEngineInstalled(cssUnrealEnginePath),
                StepStarterProject => IsStarterProjectPresent(),
                StepWwise => IsWwiseIntegrated(starterProjectPathForWwise ?? GetLastClonePath()),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVisualStudio2022Installed()
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
            return false;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = vswhere,
                Arguments = "-latest -products * -property installationPath",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process == null) return false;
            var path = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            if (process.ExitCode != 0 || string.IsNullOrEmpty(path)) return false;
            // VS 2022 is version 17.x; vswhere -latest on a 2022 install returns its path
            var versionFile = Path.Combine(path, "Common7", "IDE", "devenv.isolation.ini");
            if (!File.Exists(versionFile)) return Directory.Exists(path);
            var content = File.ReadAllText(versionFile);
            return content.Contains("17.", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private const string DefaultClangToolchainsPath = @"C:\UnrealToolchains";
    private const string DefaultClangToolchainSubdir = "v22_clang-16.0.6-centos7";

    private static bool IsClangToolchainInstalled()
    {
        var root = Environment.GetEnvironmentVariable("LINUX_MULTIARCH_ROOT");
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && HasValidClangToolchainAt(root))
            return true;
        var defaultPath = Path.Combine(DefaultClangToolchainsPath, DefaultClangToolchainSubdir);
        if (Directory.Exists(defaultPath) && HasValidClangToolchainAt(defaultPath))
            return true;
        if (!Directory.Exists(DefaultClangToolchainsPath))
            return false;
        foreach (var subDir in Directory.EnumerateDirectories(DefaultClangToolchainsPath))
        {
            if (Path.GetFileName(subDir).StartsWith("v22_clang-", StringComparison.OrdinalIgnoreCase) && HasValidClangToolchainAt(subDir))
                return true;
        }
        return false;
    }

    private static bool HasValidClangToolchainAt(string root)
    {
        var multiArch = Path.Combine(root, "x86_64-unknown-linux-gnu");
        if (Directory.Exists(multiArch))
            return true;
        var clang = Path.Combine(root, "bin", "clang++.exe");
        return File.Exists(clang);
    }

    private const string DefaultCssUnrealEnginePath = @"C:\Program Files\Unreal Engine - CSS";
    private const string UnrealBuildsRegPath = @"Software\Epic Games\Unreal Engine\Builds";

    private static bool IsCssUnrealEngineInstalled(string? configPath = null)
    {
        if (HasValidEngineAtPath(configPath?.Trim()))
            return true;
        foreach (var path in GetUnrealEnginePathsFromRegistry())
        {
            if (HasValidEngineAtPath(path))
                return true;
        }
        if (HasValidEngineAtPath(DefaultCssUnrealEnginePath))
            return true;
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        foreach (var baseDir in new[] { programFiles, programFilesX86 })
        {
            var epicDir = Path.Combine(baseDir, "Epic Games");
            if (!Directory.Exists(epicDir)) continue;
            foreach (var dir in Directory.EnumerateDirectories(epicDir))
            {
                if (HasValidEngineAtPath(dir))
                    return true;
            }
        }
        return false;
    }

    private static bool HasValidEngineAtPath(string? dir)
    {
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return false;
        var buildBat = Path.Combine(dir, "Engine", "Build", "BatchFiles", "Build.bat");
        return File.Exists(buildBat);
    }

    private static IEnumerable<string> GetUnrealEnginePathsFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
            return [];
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UnrealBuildsRegPath);
            if (key == null)
                return [];
            var cssBuilds = new List<(Version Version, string Path)>();
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (!subKeyName.EndsWith("CSS", StringComparison.OrdinalIgnoreCase))
                    continue;
                var versionPart = subKeyName[..^3].TrimEnd('-', ' '); // e.g. "5.3.2-CSS" -> "5.3.2"
                if (!Version.TryParse(versionPart, out var version))
                    continue;
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;
                var path = subKey.GetValue(null) as string ?? subKey.GetValue("") as string;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path.Trim()))
                    continue;
                cssBuilds.Add((version, path.Trim()));
            }
            if (cssBuilds.Count == 0)
                return [];
            cssBuilds.Sort((a, b) => b.Version.CompareTo(a.Version)); // highest first
            return new[] { cssBuilds[0].Path };
        }
        catch
        {
            return [];
        }
    }

    private static bool IsStarterProjectPresent()
    {
        var path = GetLastClonePath();
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return false;
        return File.Exists(Path.Combine(path, "FactoryGame.uproject"));
    }

    private static bool IsWwiseIntegrated(string? projectDir)
    {
        if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            return false;
        var wwisePlugin = Path.Combine(projectDir, "Plugins", "Wwise");
        return Directory.Exists(wwisePlugin);
    }

    /// <summary>Returns the first required step that is not detected as installed, or null if all are done.</summary>
    /// <param name="starterProjectPathForWwise">Optional path to starter project when checking step 5 (e.g. from WwiseCli:StarterProjectPath).</param>
    /// <param name="cssUnrealEnginePath">Optional path to CSS Unreal Engine when checking step 3 (e.g. from CssUnrealEngine:InstallPath).</param>
    public static int? GetFirstMissingStep(IReadOnlyList<int> requiredSteps, string? starterProjectPathForWwise = null, string? cssUnrealEnginePath = null)
    {
        foreach (var step in requiredSteps)
            if (!IsStepCompleted(step, starterProjectPathForWwise, cssUnrealEnginePath))
                return step;
        return null;
    }

    public static string GetStepName(int step) => step switch
    {
        StepVisualStudio => "1. Visual Studio 2022",
        StepClang => "2. Clang",
        StepCssUnrealEngine => "3. CSS Unreal Engine",
        StepStarterProject => "4. Starter Project",
        StepWwise => "5. Wwise",
        _ => $"Step {step}"
    };

    /// <summary>Returns true if all required steps are detected as installed; otherwise prints a message and returns false.</summary>
    /// <param name="starterProjectPathForWwise">Optional path to starter project when checking step 5.</param>
    /// <param name="cssUnrealEnginePath">Optional path to CSS Unreal Engine when checking step 3 (e.g. from CssUnrealEngine:InstallPath).</param>
    public static bool EnsureStepsCompleted(IReadOnlyList<int> requiredSteps, string? starterProjectPathForWwise = null, string? cssUnrealEnginePath = null)
    {
        var missing = GetFirstMissingStep(requiredSteps, starterProjectPathForWwise, cssUnrealEnginePath);
        if (missing == null) return true;
        AnsiConsole.MarkupLineInterpolated($"[yellow]Please complete the previous step first: {GetStepName(missing.Value)}[/]");
        return false;
    }

    public static void SetGitHubAccessToken(string? token)
    {
        Directory.CreateDirectory(StateDir);
        if (string.IsNullOrWhiteSpace(token))
            File.Delete(GitHubTokenFile);
        else
            File.WriteAllText(GitHubTokenFile, token.Trim());
    }

    public static string? GetGitHubAccessToken()
    {
        if (!File.Exists(GitHubTokenFile)) return null;
        var t = File.ReadAllText(GitHubTokenFile).Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
