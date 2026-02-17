using System.Net.Http.Headers;
using System.Text.Json;
using Spectre.Console;
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
            AnsiConsole.MarkupLine("[red]CSS Unreal Engine: set CssUnrealEngine:Repository (e.g. satisfactorymodding/UnrealEngine) or CssUnrealEngine:DownloadUrl in appsettings.json.[/]");
            return;
        }

        // Custom Unreal Engine is in a private repo; user must complete linking first.
        const string linkingDocsUrl = "https://docs.ficsit.app/satisfactory-modding/latest/Development/BeginnersGuide/dependencies.html#_link_your_github_as_an_epic_games_developer_account";
        AnsiConsole.MarkupLine("The Custom Unreal Engine is in a [yellow]private repository[/]. Before continuing you must:");
        AnsiConsole.MarkupLine("  1. Link your GitHub account as an Epic Games developer account");
        AnsiConsole.MarkupLine("  2. Link your GitHub account to the Satisfactory Modding repository (Unreal Linker)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"Full instructions: [link={linkingDocsUrl}]documentation[/]");
        AnsiConsole.WriteLine();
        var confirm = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Have you completed the linking process and are you currently logged into GitHub?")
            .AddChoices("Yes", "No"));
        if (confirm != "Yes")
        {
            AnsiConsole.MarkupLine("[yellow]Please complete the steps at the link above, then run this option again.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Use a Personal Access Token (PAT) to download here, or download and install the engine manually?")
            .AddChoices("PAT", "Manual"));
        if (choice == "Manual")
        {
            const string releasesUrl = "https://github.com/satisfactorymodding/UnrealEngine/releases/latest";
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Download and install the engine yourself:");
            AnsiConsole.MarkupLineInterpolated($"  1. Open: [link={releasesUrl}]releases[/]");
            AnsiConsole.MarkupLine("  2. Download all the .bin files and the .exe installer.");
            AnsiConsole.MarkupLine("  3. Put them in the same folder.");
            AnsiConsole.MarkupLine("  4. Run the .exe when everything is downloaded.");
            AnsiConsole.WriteLine();
            return;
        }

        // PAT path: resolve token from saved, then config/env, then prompt (OAuth code left in repo for possible future use)
        var token = SmehState.GetGitHubAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            token = Environment.GetEnvironmentVariable("SMEH_GITHUB_PAT")?.Trim() ?? _options.GitHubPat?.Trim();
            if (!string.IsNullOrEmpty(token))
                AnsiConsole.MarkupLine("[dim]Using GitHub PAT from config or SMEH_GITHUB_PAT.[/]");
        }

        if (string.IsNullOrEmpty(token))
        {
            token = AnsiConsole.Prompt(new TextPrompt<string>("Enter your GitHub Personal Access Token (PAT with repo scope):")
                .Secret());
            if (string.IsNullOrEmpty(token?.Trim()))
            {
                AnsiConsole.MarkupLine("[red]No token entered. Aborted.[/]");
                return;
            }
            token = token.Trim();
            SmehState.SetGitHubAccessToken(token);
            AnsiConsole.MarkupLine("[green]PAT saved for future runs.[/]");
        }

        var releaseUrl = $"https://api.github.com/repos/{repo}/releases/latest";
        AnsiConsole.MarkupLine("[dim]Fetching latest release info...[/]");

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
                        AnsiConsole.MarkupLine("[green]Using saved PAT from config; access succeeded.[/]");
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    AnsiConsole.WriteLine();
                    var enteredPat = AnsiConsole.Prompt(new TextPrompt<string>("Enter your GitHub Personal Access Token (PAT with repo scope):")
                        .Secret()
                        .AllowEmpty());
                    if (!string.IsNullOrEmpty(enteredPat?.Trim()))
                    {
                        response.Dispose();
                        authClient.Dispose();
                        authClient = CreateGitHubHttpClient(enteredPat.Trim());
                        response = await authClient.GetAsync(releaseUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            SmehState.SetGitHubAccessToken(enteredPat.Trim());
                            AnsiConsole.MarkupLine("[green]PAT saved for future runs.[/]");
                        }
                    }
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    AnsiConsole.MarkupLine("[red]Could not access the repository (404). This usually means:[/]");
                    AnsiConsole.MarkupLine("  - The repo is private and no valid GitHub authorization is present.");
                    AnsiConsole.MarkupLine("  - You have not finished linking your GitHub account (see the link above).");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("To authorize: set [dim]CssUnrealEngine:GitHubOAuthClientId[/] for OAuth (device flow), or [dim]CssUnrealEngine:GitHubPat[/] / [dim]SMEH_GITHUB_PAT[/] for a PAT with repo scope.");
                    AnsiConsole.MarkupLine("[link=https://github.com/settings/developers]OAuth App[/] — callback URL http://localhost, enable Device flow.");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("Or download the release files manually in your browser, save the .exe and .bin in one folder, then run the .exe.");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var v) ? v.FirstOrDefault() : null;
                    var body = await response.Content.ReadAsStringAsync();
                    AnsiConsole.MarkupLine("[red]Access to the repository was denied (403 Forbidden).[/]");
                    if (remaining == "0" || (body.Length > 0 && body.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        AnsiConsole.MarkupLine("[yellow]GitHub rate limit may be exceeded. Wait a few minutes and try again.[/]");
                        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && long.TryParse(reset.FirstOrDefault(), out var resetEpoch))
                            AnsiConsole.MarkupLineInterpolated($"[dim]Rate limit resets at: {DateTimeOffset.FromUnixTimeSeconds(resetEpoch):u}[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("If you have already authorized this app for the satisfactorymodding org, 403 can still mean:");
                        AnsiConsole.MarkupLine("  - Rate limiting (wait and retry), or");
                        AnsiConsole.MarkupLine("  - The repo or org requires additional approval.");
                        if (!string.IsNullOrWhiteSpace(body) && body.Length < 500)
                            AnsiConsole.MarkupLineInterpolated($"[dim]GitHub says: {Markup.Escape(body)}[/]");
                    }
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("Alternatively, use [dim]CssUnrealEngine:DownloadUrl[/] in appsettings.json to point to a direct download.");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Failed to get release: {response.StatusCode}. Check Repository or try DownloadUrl.[/]");
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
            AnsiConsole.MarkupLine("[red]No .exe installer found in this release.[/]");
            return false;
        }

        var installDir = Path.Combine(Path.GetTempPath(), "SMEH", "CssUnrealEngine");
        Directory.CreateDirectory(installDir);

        var ghHeaders = new Dictionary<string, string> { ["Accept"] = "application/octet-stream" };
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p));

        AnsiConsole.MarkupLine("[dim]Downloading installer (.exe)...[/]");
        var exePath = Path.Combine(installDir, exeAsset);
        await _downloadHelper.DownloadFileAsync(exeApiUrl, exePath, progress, default, authClient, ghHeaders);
        ConsoleProgressBar.Clear();
        AnsiConsole.WriteLine();

        foreach (var (name, apiUrl) in binAssets)
        {
            AnsiConsole.MarkupLineInterpolated($"[dim]Downloading {Markup.Escape(name)}...[/]");
            var binPath = Path.Combine(installDir, name);
            await _downloadHelper.DownloadFileAsync(apiUrl, binPath, progress, default, authClient, ghHeaders);
            ConsoleProgressBar.Clear();
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[dim]All files ready. Running installer...[/]");
        var result = await _processRunner.RunAsync(exePath, null, installDir, waitForExit: true);
        if (result.ExitCode != 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
        else
            AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
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

        AnsiConsole.MarkupLine("[dim]Downloading CSS Unreal Engine...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "CSS UE"));
        await _downloadHelper.DownloadFileAsync(downloadUrl, destPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete.[/]");

        var isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (isZip)
        {
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            AnsiConsole.MarkupLine("[dim]Extracting...[/]");
            System.IO.Compression.ZipFile.ExtractToDirectory(destPath, extractDir, overwriteFiles: true);
            AnsiConsole.MarkupLineInterpolated($"[dim]Extracted to: {Markup.Escape(extractDir)}[/]");
        }
        else if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[dim]Running installer...[/]");
            var result = await _processRunner.RunAsync(destPath, null, tempDir, waitForExit: true);
            if (result.ExitCode != 0)
                AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
            else
                AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[dim]Downloaded to: {Markup.Escape(destPath)}[/]");
            AnsiConsole.MarkupLine("[yellow]Unknown file type. Extract or run manually as needed.[/]");
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
