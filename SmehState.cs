using System.Diagnostics;
using Microsoft.Win32;
using Spectre.Console;

namespace SMEH;

/// <summary>Tracks paths for the current app run and detects installed steps.</summary>
public static class SmehState
{
    /// <summary>When true, run-all flow is active; services should skip interactive prompts and use pre-set paths.</summary>
    public static bool RunAllUnattended { get; set; }

    /// <summary>Folder containing the UE installer files (Manual path), set during run-all so we can offer to delete at the end without asking for path.</summary>
    public static string? LastUnrealEngineInstallerFolder { get; set; }

    /// <summary>Step numbers: 1=VS 2022, 2=Clang, 3=CSS Unreal Engine, 4=Starter Project, 5=Wwise.</summary>
    public const int StepVisualStudio = 1;
    public const int StepClang = 2;
    public const int StepCssUnrealEngine = 3;
    public const int StepStarterProject = 4;
    public const int StepWwise = 5;

    private static string? LastClonePath { get; set; }

    private static string LegacyStateDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMEH");

    private static string LegacyLastClonePathFile => Path.Combine(LegacyStateDir, "last-clone-path.txt");

    public static void ClearLegacyPersistedState()
    {
        try
        {
            if (File.Exists(LegacyLastClonePathFile))
                File.Delete(LegacyLastClonePathFile);

            if (Directory.Exists(LegacyStateDir) && !Directory.EnumerateFileSystemEntries(LegacyStateDir).Any())
                Directory.Delete(LegacyStateDir);
        }
        catch
        {

        }
    }

    public static void SetLastClonePath(string cloneDirectory)
    {
        LastClonePath = cloneDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string? GetLastClonePath()
    {
        var path = LastClonePath?.Trim();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>Returns true if the given step is detected as installed.</summary>
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

    public static bool TryGetVisualStudio2022InstallPath(out string? installationPath)
    {
        installationPath = null;
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
                Arguments = "-latest -version [17.0,18.0) -products * -property installationPath",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process == null) return false;
            var path = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            if (process.ExitCode != 0 || string.IsNullOrEmpty(path)) return false;
            if (!Directory.Exists(path)) return false;
            installationPath = path;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsVisualStudio2022Installed() => TryGetVisualStudio2022InstallPath(out _);

    private static bool IsClangToolchainInstalled()
    {
        var root = Environment.GetEnvironmentVariable("LINUX_MULTIARCH_ROOT");
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && HasValidClangToolchainAt(root))
            return true;
        var defaultPath = Path.Combine(AppDefaults.ClangToolchainsPath, AppDefaults.ClangToolchainSubdir);
        if (Directory.Exists(defaultPath) && HasValidClangToolchainAt(defaultPath))
            return true;
        if (!Directory.Exists(AppDefaults.ClangToolchainsPath))
            return false;
        foreach (var subDir in Directory.EnumerateDirectories(AppDefaults.ClangToolchainsPath))
        {
            if (Path.GetFileName(subDir).StartsWith("v25_clang-", StringComparison.OrdinalIgnoreCase) && HasValidClangToolchainAt(subDir))
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
        if (HasValidEngineAtPath(AppDefaults.CssUnrealEngineInstallPath))
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
                var versionPart = subKeyName[..^3].TrimEnd('-', ' ');
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
            cssBuilds.Sort((a, b) => b.Version.CompareTo(a.Version));
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
        return File.Exists(Path.Combine(path, AppDefaults.StarterProjectFileName));
    }

    private static bool IsWwiseIntegrated(string? projectDir)
    {
        if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
            return false;
        var wwisePlugin = Path.Combine(projectDir, "Plugins", "Wwise");
        return Directory.Exists(wwisePlugin);
    }

    /// <summary>Returns the first required step that is not detected as installed, or null if all are done.</summary>
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
    public static bool EnsureStepsCompleted(IReadOnlyList<int> requiredSteps, string? starterProjectPathForWwise = null, string? cssUnrealEnginePath = null)
    {
        var missing = GetFirstMissingStep(requiredSteps, starterProjectPathForWwise, cssUnrealEnginePath);
        if (missing == null) return true;
        AnsiConsole.MarkupLineInterpolated($"[yellow]Please complete the previous step first: {GetStepName(missing.Value)}[/]");
        return false;
    }

}
