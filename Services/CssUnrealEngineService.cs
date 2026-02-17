using System.Net.Http.Headers;
using System.Text.Json;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

public class CssUnrealEngineService
{
    private readonly CssUnrealEngineOptions _options;
    private readonly DownloadHelper _downloadHelper;
    private readonly ProcessRunner _processRunner;

    public CssUnrealEngineService(CssUnrealEngineOptions options, DownloadHelper downloadHelper, ProcessRunner processRunner)
    {
        _options = options;
        _downloadHelper = downloadHelper;
        _processRunner = processRunner;
    }

    public async Task RunAsync()
    {
        if (!SmehState.EnsureStepsCompleted(new[] { SmehState.StepVisualStudio, SmehState.StepClang }))
            return;

        var downloadUrl = _options.DownloadUrl?.Trim();
        if (!string.IsNullOrEmpty(downloadUrl))
        {
            await RunLegacyDownloadAsync(downloadUrl);
            return;
        }

        var repo = _options.Repository?.Trim();
        if (string.IsNullOrEmpty(repo))
        {
            Console.WriteLine("CSS Unreal Engine: set CssUnrealEngine:Repository (e.g. satisfactorymodding/UnrealEngine) or CssUnrealEngine:DownloadUrl in appsettings.json.");
            return;
        }

        // Custom Unreal Engine is in a private repo; user must complete linking first.
        const string linkingDocsUrl = "https://docs.ficsit.app/satisfactory-modding/latest/Development/BeginnersGuide/dependencies.html#_link_your_github_as_an_epic_games_developer_account";
        Console.WriteLine("The Custom Unreal Engine is in a private repository. Before continuing you must:");
        Console.WriteLine("  1. Link your GitHub account as an Epic Games developer account");
        Console.WriteLine("  2. Link your GitHub account to the Satisfactory Modding repository (Unreal Linker)");
        Console.WriteLine();
        Console.WriteLine($"Full instructions: {linkingDocsUrl}");
        Console.WriteLine();
        Console.Write("Have you completed the linking process and are you currently logged into GitHub? (y/n): ");
        var confirm = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (confirm != "Y" && confirm != "YES")
        {
            Console.WriteLine("Please complete the steps at the link above, then run this option again.");
            return;
        }

        Console.WriteLine();
        Console.Write("Use a Personal Access Token (PAT) to download here, or download and install the engine manually? (PAT / manual): ");
        var choice = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (choice == "MANUAL" || choice == "M")
        {
            const string releasesUrl = "https://github.com/satisfactorymodding/UnrealEngine/releases/latest";
            Console.WriteLine();
            Console.WriteLine("Download and install the engine yourself:");
            Console.WriteLine($"  1. Open: {releasesUrl}");
            Console.WriteLine("  2. Download all the .bin files and the .exe installer.");
            Console.WriteLine("  3. Put them in the same folder.");
            Console.WriteLine("  4. Run the .exe when everything is downloaded.");
            Console.WriteLine();
            return;
        }

        // PAT path: resolve token from saved, then config/env, then prompt (OAuth code left in repo for possible future use)
        var token = SmehState.GetGitHubAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            token = Environment.GetEnvironmentVariable("SMEH_GITHUB_PAT")?.Trim() ?? _options.GitHubPat?.Trim();
            if (!string.IsNullOrEmpty(token))
                Console.WriteLine("Using GitHub PAT from config or SMEH_GITHUB_PAT.");
        }

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("Enter your GitHub Personal Access Token (PAT with repo scope) and press Enter:");
            token = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("No token entered. Aborted.");
                return;
            }
            SmehState.SetGitHubAccessToken(token);
            Console.WriteLine("PAT saved for future runs.");
        }

        var releaseUrl = $"https://api.github.com/repos/{repo}/releases/latest";
        Console.WriteLine("Fetching latest release info...");

        var authClient = CreateGitHubHttpClient(token);
        try
        {
            var response = await authClient.GetAsync(releaseUrl);

            if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.Forbidden && !IsRateLimitResponse(response))
            {
                var configPat = Environment.GetEnvironmentVariable("SMEH_GITHUB_PAT")?.Trim() ?? _options.GitHubPat?.Trim();
                if (!string.IsNullOrEmpty(configPat) && configPat != token)
                {
                    response.Dispose();
                    authClient.Dispose();
                    authClient = CreateGitHubHttpClient(configPat);
                    response = await authClient.GetAsync(releaseUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        SmehState.SetGitHubAccessToken(configPat);
                        Console.WriteLine("Using saved PAT from config; access succeeded.");
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine();
                    Console.WriteLine("Enter your GitHub Personal Access Token (PAT with repo scope) and press Enter:");
                    var enteredPat = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(enteredPat))
                    {
                        response.Dispose();
                        authClient.Dispose();
                        authClient = CreateGitHubHttpClient(enteredPat);
                        response = await authClient.GetAsync(releaseUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            SmehState.SetGitHubAccessToken(enteredPat);
                            Console.WriteLine("PAT saved for future runs.");
                        }
                    }
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("Could not access the repository (404). This usually means:");
                    Console.WriteLine("  - The repo is private and no valid GitHub authorization is present.");
                    Console.WriteLine("  - You have not finished linking your GitHub account (see the link above).");
                    Console.WriteLine();
                    Console.WriteLine("To authorize: set CssUnrealEngine:GitHubOAuthClientId for OAuth (device flow), or set CssUnrealEngine:GitHubPat / SMEH_GITHUB_PAT for a PAT with repo scope.");
                    Console.WriteLine("OAuth App: https://github.com/settings/developers — callback URL http://localhost, enable Device flow.");
                    Console.WriteLine();
                    Console.WriteLine("Or download the release files manually in your browser, save the .exe and .bin in one folder, then run the .exe.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var v) ? v.FirstOrDefault() : null;
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Access to the repository was denied (403 Forbidden).");
                    if (remaining == "0" || (body.Length > 0 && body.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Console.WriteLine("GitHub rate limit may be exceeded. Wait a few minutes and try again.");
                        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out var resetEpoch))
                            Console.WriteLine($"Rate limit resets at: {DateTimeOffset.FromUnixTimeSeconds(resetEpoch):u}");
                    }
                    else
                    {
                        Console.WriteLine("If you have already authorized this app for the satisfactorymodding org, 403 can still mean:");
                        Console.WriteLine("  - Rate limiting (wait and retry), or");
                        Console.WriteLine("  - The repo or org requires additional approval.");
                        if (!string.IsNullOrWhiteSpace(body) && body.Length < 500)
                            Console.WriteLine($"  GitHub says: {body}");
                    }
                    Console.WriteLine();
                    Console.WriteLine("Alternatively, use CssUnrealEngine:DownloadUrl in appsettings.json to point to a direct download.");
                }
                else
                {
                    Console.WriteLine($"Failed to get release: {response.StatusCode}. Check Repository or try DownloadUrl.");
                }
                response.Dispose();
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            response.Dispose();
            await DownloadAndInstallFromReleaseJson(authClient, json);
        }
        finally
        {
            authClient?.Dispose();
        }
    }

    private static bool IsRateLimitResponse(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var v) && v.FirstOrDefault() == "0")
            return true;
        return false;
    }

    private async Task<bool> DownloadAndInstallFromReleaseJson(HttpClient authClient, string releaseJson)
    {
        using var doc = JsonDocument.Parse(releaseJson);
        var root = doc.RootElement;
        var assets = root.GetProperty("assets");

        var exeAsset = (string?)null;
        var exeApiUrl = (string?)null;
        var binAssets = new List<(string Name, string ApiUrl)>();

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var apiUrl = asset.GetProperty("url").GetString() ?? "";
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                exeAsset = name;
                exeApiUrl = apiUrl;
            }
            else if (name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            {
                binAssets.Add((name, apiUrl));
            }
        }

        if (string.IsNullOrEmpty(exeApiUrl) || string.IsNullOrEmpty(exeAsset))
        {
            Console.WriteLine("No .exe installer found in this release.");
            return false;
        }

        var installDir = Path.Combine(Path.GetTempPath(), "SMEH", "CssUnrealEngine");
        Directory.CreateDirectory(installDir);

        var ghHeaders = new Dictionary<string, string> { ["Accept"] = "application/octet-stream" };
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p));

        Console.WriteLine("Downloading installer (.exe)...");
        var exePath = Path.Combine(installDir, exeAsset);
        await _downloadHelper.DownloadFileAsync(exeApiUrl, exePath, progress, default, authClient, ghHeaders);
        ConsoleProgressBar.Clear();
        Console.WriteLine();

        foreach (var (name, apiUrl) in binAssets)
        {
            Console.WriteLine($"Downloading {name}...");
            var binPath = Path.Combine(installDir, name);
            await _downloadHelper.DownloadFileAsync(apiUrl, binPath, progress, default, authClient, ghHeaders);
            ConsoleProgressBar.Clear();
            Console.WriteLine();
        }

        Console.WriteLine("All files ready. Running installer...");
        var result = await _processRunner.RunAsync(exePath, null, installDir, waitForExit: true);
        if (result.ExitCode != 0)
            Console.WriteLine($"Installer exited with code {result.ExitCode}.");
        else
            Console.WriteLine("Installation finished successfully.");
        return true;
    }

    private async Task RunLegacyDownloadAsync(string downloadUrl)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SMEH", "CssUnrealEngine");
        Directory.CreateDirectory(tempDir);

        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrEmpty(fileName))
            fileName = "css-unreal-engine.zip";
        var destPath = Path.Combine(tempDir, fileName);

        Console.WriteLine("Downloading CSS Unreal Engine...");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "CSS UE"));
        await _downloadHelper.DownloadFileAsync(downloadUrl, destPath, progress);
        ConsoleProgressBar.Clear();
        Console.WriteLine("Download complete.");

        var isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (isZip)
        {
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            Console.WriteLine("Extracting...");
            System.IO.Compression.ZipFile.ExtractToDirectory(destPath, extractDir, overwriteFiles: true);
            Console.WriteLine($"Extracted to: {extractDir}");
        }
        else if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Running installer...");
            var result = await _processRunner.RunAsync(destPath, null, tempDir, waitForExit: true);
            if (result.ExitCode != 0)
                Console.WriteLine($"Installer exited with code {result.ExitCode}.");
            else
                Console.WriteLine("Installation finished successfully.");
        }
        else
        {
            Console.WriteLine($"Downloaded to: {destPath}");
            Console.WriteLine("Unknown file type. Extract or run manually as needed.");
        }
    }

    private static HttpClient CreateGitHubHttpClient(string? token)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SMEH/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        return client;
    }
}
