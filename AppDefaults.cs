namespace SMEH;

/// <summary>Default URLs and paths used when no config file is present. Edit these to change app behavior.</summary>
public static class AppDefaults
{
    // Visual Studio
    public const string VisualStudioBootstrapperUrl = "https://aka.ms/vs/17/release/vs_community.exe";
    public const string VisualStudioConfigFileUrl = "https://docs.ficsit.app/satisfactory-modding/latest/_attachments/BeginnersGuide/dependencies/SML.vsconfig";

    // Clang
    public const string ClangInstallerUrl = "https://cdn.unrealengine.com/CrossToolchain_Linux/v22_clang-16.0.6-centos7.exe";

    // CSS Unreal Engine
    public const string CssUnrealEngineRepository = "satisfactorymodding/UnrealEngine";
    public const string CssUnrealEngineInstallPath = @"C:\Program Files\Unreal Engine - CSS";

    // Wwise-CLI
    public const string WwiseCliRepository = "mircearoata/wwise-cli";
    public const string WwiseCliReleaseTag = "v0.2.2";
    public const string WwiseCliSdkVersion = "2023.1.3.8471";
    public const string WwiseCliIntegrationVersion = "2023.1.3.2970";

    // Starter Project
    public const string StarterProjectRepositoryUrl = "https://github.com/satisfactorymodding/SatisfactoryModLoader.git";
    public const string StarterProjectBranch = "master";

    public static SmehOptions CreateOptions()
    {
        return new SmehOptions
        {
            VisualStudio = new VisualStudioOptions
            {
                BootstrapperUrl = VisualStudioBootstrapperUrl,
                ConfigFilePath = "",
                ConfigFileUrl = VisualStudioConfigFileUrl
            },
            Clang = new ClangOptions
            {
                InstallerUrl = ClangInstallerUrl
            },
            CssUnrealEngine = new CssUnrealEngineOptions
            {
                Repository = CssUnrealEngineRepository,
                DownloadUrl = "",
                InstallPath = CssUnrealEngineInstallPath,
                GitHubOAuthClientId = "",
                GitHubPat = ""
            },
            WwiseCli = new WwiseCliOptions
            {
                UseLatest = true,
                ReleaseTag = WwiseCliReleaseTag,
                Repository = WwiseCliRepository,
                SdkVersion = WwiseCliSdkVersion,
                IntegrationVersion = WwiseCliIntegrationVersion,
                StarterProjectPath = ""
            },
            StarterProject = new StarterProjectOptions
            {
                RepositoryUrl = StarterProjectRepositoryUrl,
                Branch = StarterProjectBranch,
                DefaultClonePath = ""
            }
        };
    }
}
