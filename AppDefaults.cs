namespace SMEH;

public static class AppDefaults
{
    // Visual Studio
    public const string VisualStudioBootstrapperUrl = "https://aka.ms/vs/17/release/vs_community.exe";
    public const string VisualStudioBootstrapperFileName = "vs_community.exe";
    public const string VisualStudioConfigFileUrl = "https://docs.ficsit.app/satisfactory-modding/latest/_attachments/BeginnersGuide/dependencies/SML.vsconfig";
    public const string VisualStudioConfigFileName = "SML.vsconfig";
    public const string VisualStudioInstallerFileName = "setup.exe";

    // Clang
    public const string ClangInstallerUrl = "https://cdn.unrealengine.com/CrossToolchain_Linux/v25_clang-18.1.0-rockylinux8.exe";
    public const string ClangInstallerFileName = "v25_clang-18.1.0-rockylinux8.exe";
    public const string ClangToolchainsPath = @"C:\UnrealToolchains";
    public const string ClangToolchainSubdir = "v25_clang-18.1.0-rockylinux8";

    public const string DirectXWebInstallerUrl = "https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe";
    public const string DirectXWebInstallerFileName = "dxwebsetup.exe";

    public const string VcRedistX64Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
    public const string VcRedistX64FileName = "vc_redist.x64.exe";

    // CSS Unreal Engine
    public const string CssUnrealEngineRepository = "satisfactorymodding/UnrealEngine";
    public const string CssUnrealEngineInstallPath = @"C:\Program Files\Unreal Engine - CSS";
    public const string CssUnrealEngineLinkingDocsUrl = "https://docs.ficsit.app/satisfactory-modding/latest/Development/BeginnersGuide/dependencies.html#_link_your_github_as_an_epic_games_developer_account";
    public const string CssUnrealEngineReleasesUrl = "https://github.com/satisfactorymodding/UnrealEngine/releases";
    public const string CssUnrealEngineInstallerFileName = "UnrealEngine-CSS-Editor-Win64.exe";
    public const string CssUnrealEngineInstallerBinPattern = "UnrealEngine-CSS-Editor-Win64-*.bin";

    // Wwise-CLI
    public const string WwiseCliRepository = "mircearoata/wwise-cli";
    public const string WwiseCliReleaseTag = "v0.2.2";
    public const string WwiseCliSdkVersion = "2023.1.14.8770";
    public const string WwiseCliIntegrationVersion = "2023.1.14.3555";

    // Starter Project
    public const string StarterProjectRepositoryUrl = "https://github.com/satisfactorymodding/SatisfactoryModLoader.git";
    public const string StarterProjectDirectoryName = "SatisfactoryModLoader";
    public const string StarterProjectBranch = "master";
    public const string StarterProjectDevBranch = "dev";
    public const string StarterProjectFileName = "FactoryGame.uproject";
    public const string ProjectSetupDocsUrl = "https://docs.ficsit.app/satisfactory-modding/latest/Development/BeginnersGuide/project_setup.html#_open_unreal_editor";

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
                InstallPath = CssUnrealEngineInstallPath
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
