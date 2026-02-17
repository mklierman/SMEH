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
    /// <summary>Ignored: the app always installs Visual Studio 2022 Community Edition. Kept for config compatibility.</summary>
    public string BootstrapperUrl { get; set; } = "https://aka.ms/vs/17/release/vs_community.exe";
    /// <summary>Optional local path to a .vsconfig file. If set and the file exists, this is used instead of ConfigFileUrl.</summary>
    public string ConfigFilePath { get; set; } = "";
    /// <summary>URL to download a .vsconfig file (e.g. SML workload config). Used when ConfigFilePath is not set.</summary>
    public string ConfigFileUrl { get; set; } = "https://docs.ficsit.app/satisfactory-modding/latest/_attachments/BeginnersGuide/dependencies/SML.vsconfig";
}

public class ClangOptions
{
    public string InstallerUrl { get; set; } = "https://cdn.unrealengine.com/CrossToolchain_Linux/v22_clang-16.0.6-centos7.exe";
}

public class CssUnrealEngineOptions
{
    /// <summary>GitHub repo for custom Unreal Engine (e.g. satisfactorymodding/UnrealEngine). Latest release is used.</summary>
    public string Repository { get; set; } = "satisfactorymodding/UnrealEngine";
    /// <summary>Optional: direct download URL override. If set, skips GitHub and downloads this single file (legacy).</summary>
    public string DownloadUrl { get; set; } = "";
    /// <summary>Path where CSS Unreal Engine is or will be installed. Default is the installer default. Set this if you chose a different path during install.</summary>
    public string InstallPath { get; set; } = @"C:\Program Files\Unreal Engine - CSS";
    /// <summary>GitHub OAuth App Client ID for device flow. Create one at https://github.com/settings/developers (no secret needed).
    /// When asked for "Authorization callback URL", use: http://localhost (not used for device flow). Enables downloading from the private repo after you authorize in the browser (with 2FA).</summary>
    public string GitHubOAuthClientId { get; set; } = "";
    /// <summary>Optional fallback: GitHub Personal Access Token (PAT) with repo (and read:org if needed) scope. Used when no OAuth token is available. Prefer env var SMEH_GITHUB_PAT to avoid storing in config.</summary>
    public string GitHubPat { get; set; } = "";
}

public class WwiseCliOptions
{
    public bool UseLatest { get; set; } = true;
    public string ReleaseTag { get; set; } = "v0.2.2";
    public string Repository { get; set; } = "mircearoata/wwise-cli";
    public string SdkVersion { get; set; } = "2023.1.3.8471";
    public string IntegrationVersion { get; set; } = "2023.1.3.2970";
    /// <summary>Path to SatisfactoryModLoader clone (containing FactoryGame.uproject). If empty, uses last clone from option 5 or prompts.</summary>
    public string StarterProjectPath { get; set; } = "";
}

public class StarterProjectOptions
{
    public string RepositoryUrl { get; set; } = "https://github.com/satisfactorymodding/SatisfactoryModLoader.git";
    public string Branch { get; set; } = "master";
    public string DefaultClonePath { get; set; } = "";
}
