namespace SMEH;

public class SmehOptions
{
    public const string SectionName = "Smeh";

    public VisualStudioOptions VisualStudio { get; set; } = new();
    public ClangOptions Clang { get; set; } = new();
    public CssUnrealEngineOptions CssUnrealEngine { get; set; } = new();
    public WwiseCliOptions WwiseCli { get; set; } = new();
    public StarterProjectOptions StarterProject { get; set; } = new();
}

public class VisualStudioOptions
{
    public string BootstrapperUrl { get; set; } = AppDefaults.VisualStudioBootstrapperUrl;
    public string ConfigFilePath { get; set; } = "";
    public string ConfigFileUrl { get; set; } = AppDefaults.VisualStudioConfigFileUrl;
}

public class ClangOptions
{
    public string InstallerUrl { get; set; } = AppDefaults.ClangInstallerUrl;
}

public class CssUnrealEngineOptions
{
    public string Repository { get; set; } = AppDefaults.CssUnrealEngineRepository;
    public string InstallPath { get; set; } = AppDefaults.CssUnrealEngineInstallPath;
    public string EffectiveInstallPath => SetupOptionDefaults.ValueOrDefault(InstallPath, AppDefaults.CssUnrealEngineInstallPath);
}

public class WwiseCliOptions
{
    public bool UseLatest { get; set; } = true;
    public string ReleaseTag { get; set; } = AppDefaults.WwiseCliReleaseTag;
    public string Repository { get; set; } = AppDefaults.WwiseCliRepository;
    public string SdkVersion { get; set; } = AppDefaults.WwiseCliSdkVersion;
    public string IntegrationVersion { get; set; } = AppDefaults.WwiseCliIntegrationVersion;
    public string EffectiveSdkVersion => SetupOptionDefaults.ValueOrDefault(SdkVersion, AppDefaults.WwiseCliSdkVersion);
    public string EffectiveIntegrationVersion => SetupOptionDefaults.ValueOrDefault(IntegrationVersion, AppDefaults.WwiseCliIntegrationVersion);
    public string StarterProjectPath { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class StarterProjectOptions
{
    public string RepositoryUrl { get; set; } = AppDefaults.StarterProjectRepositoryUrl;
    public string Branch { get; set; } = AppDefaults.StarterProjectBranch;
    public string DefaultClonePath { get; set; } = "";
}

internal static class SetupOptionDefaults
{
    public static string ValueOrDefault(string? value, string defaultValue)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? defaultValue : trimmed;
    }
}
