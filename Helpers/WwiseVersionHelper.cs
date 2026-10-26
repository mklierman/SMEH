using System.Text.RegularExpressions;

namespace SMEH.Helpers;

/// <summary>
/// Reads the Wwise SDK and integration versions an SML checkout expects from its CI workflow,
/// so the installed Wwise matches the patches in Mods/WwisePatches for that branch.
/// </summary>
public static partial class WwiseVersionHelper
{
    private static readonly string[] WorkflowRelativePath = { ".github", "workflows", "build.yml" };

    [GeneratedRegex(@"--sdk-version\s+[""']?(?<version>\d+(?:\.\d+){3})[""']?")]
    private static partial Regex SdkVersionRegex();

    [GeneratedRegex(@"--integration-version\s+[""']?(?<version>\d+(?:\.\d+){3})[""']?")]
    private static partial Regex IntegrationVersionRegex();

    public static bool TryReadFromProject(string projectDir, out string sdkVersion, out string integrationVersion)
    {
        sdkVersion = "";
        integrationVersion = "";

        var workflowPath = Path.Combine(new[] { projectDir }.Concat(WorkflowRelativePath).ToArray());
        if (!File.Exists(workflowPath))
        {
            return false;
        }

        string content;
        try
        {
            content = File.ReadAllText(workflowPath);
        }
        catch (IOException)
        {
            return false;
        }

        var sdkMatch = SdkVersionRegex().Match(content);
        var integrationMatch = IntegrationVersionRegex().Match(content);
        if (!sdkMatch.Success || !integrationMatch.Success)
        {
            return false;
        }

        sdkVersion = sdkMatch.Groups["version"].Value;
        integrationVersion = integrationMatch.Groups["version"].Value;
        return true;
    }
}
