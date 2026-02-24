using System.Net.Http.Headers;
using System.Text.Json;
using Spectre.Console;
using SMEH.Helpers;
using SMEH;

namespace SMEH.Services;

/// <summary>Downloads and installs the CSS (Custom Source Satisfactory) Unreal Engine build; menu option 2. Runs DirectX and VC Redist installers as prerequisites.</summary>
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

    private const string UnrealEngineInstallerArgs = "/SILENT /NORESTART";
    private static readonly string DefaultInstallPath = AppDefaults.CssUnrealEngineInstallPath;
    private const string ManualInstallExe = "UnrealEngine-CSS-Editor-Win64.exe";
    private const string ManualInstallBin1 = "UnrealEngine-CSS-Editor-Win64-1.bin";
    private const string ManualInstallBin2 = "UnrealEngine-CSS-Editor-Win64-2.bin";

    private async Task RunDirectXAndVcRedistAsync()
    {
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing DirectX End-User Runtime (required for Unreal Engine)...[/]");
        var directXService = new DirectXRuntimeService(_downloadHelper, _processRunner);
        if (!await directXService.RunAsync())
            AnsiConsole.MarkupLine("[yellow]DirectX install failed or was skipped. Unreal Engine may show XINPUT1_3.dll errors. Continuing with UE install.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Installing Visual C++ Redistributable 2015-2022 (x64) (required for Unreal Engine)...[/]");
        var vcRedistService = new VcRedistService(_downloadHelper, _processRunner);
        if (!await vcRedistService.RunAsync())
            AnsiConsole.MarkupLine("[yellow]VC++ Redist install failed or was skipped. Continuing with UE install.[/]");
        AnsiConsole.WriteLine();
    }

    public async Task<bool> RunAsync()
    {
        var downloadUrl = _options.DownloadUrl?.Trim();
        var repo = _options.Repository?.Trim();
        if (string.IsNullOrEmpty(downloadUrl) && string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]CSS Unreal Engine: Repository or DownloadUrl is not set.[/]");
            var input = AnsiConsole.Prompt(new TextPrompt<string>("Enter GitHub repository (e.g. satisfactorymodding/UnrealEngine) or direct download URL (or press Enter to cancel):")
                .AllowEmpty());
            if (string.IsNullOrWhiteSpace(input))
            {
                AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
                return false;
            }
            input = input!.Trim();
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                downloadUrl = input;
            else
                repo = input;
        }

        if (!string.IsNullOrEmpty(downloadUrl))
        {
            await RunDirectXAndVcRedistAsync();
            return await RunLegacyDownloadAsync(downloadUrl);
        }

        if (string.IsNullOrEmpty(repo))
        {
            AnsiConsole.MarkupLine("[red]Repository is required.[/]");
            return false;
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
            .Title("Have you completed the linking process?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (confirm != "Yes")
        {
            AnsiConsole.MarkupLine("[yellow]Please complete the steps at the link above, then run this option again.[/]");
            return false;
        }

        AnsiConsole.WriteLine();
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Use a Personal Access Token (PAT) to download here, or download and install the engine manually?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Manual", "PAT"));
        if (choice == "Manual")
        {
            const string releasesUrl = "https://github.com/satisfactorymodding/UnrealEngine/releases/latest";
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Download and install the engine yourself:");
            AnsiConsole.MarkupLineInterpolated($"  1. Open: [link={releasesUrl}]releases[/]");
            AnsiConsole.MarkupLine("  2. Download the .exe and .bin files (e.g. UnrealEngine-CSS-Editor-Win64.exe and its .bin parts).");
            AnsiConsole.WriteLine();
            var runNow = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("Have you downloaded all the files? Do you want to run the installer?")
                .HighlightStyle(SmehTheme.AccentStyle)
                .AddChoices("Yes", "No"));
            if (runNow == "No")
                return false;
            var installFolder = GetManualInstallFolder();
            if (string.IsNullOrEmpty(installFolder))
                return false;
            await RunDirectXAndVcRedistAsync();
            return await RunInstallerFromFolderAsync(installFolder);
        }

        // PAT path: resolve token from saved, then config/env, then prompt (OAuth code left in repo for possible future use)
        var token = SmehState.GetGitHubAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            token = Environment.GetEnvironmentVariable("SMEH_GITHUB_PAT")?.Trim() ?? _options.GitHubPat?.Trim();
            if (!string.IsNullOrEmpty(token))
                AnsiConsole.MarkupLine($"[dim]Using GitHub PAT from config or SMEH_GITHUB_PAT.[/]");
        }

        if (string.IsNullOrEmpty(token))
        {
            token = AnsiConsole.Prompt(new TextPrompt<string>("Enter your GitHub Personal Access Token (PAT with repo scope):")
                .Secret());
            if (string.IsNullOrEmpty(token?.Trim()))
            {
                AnsiConsole.MarkupLine("[red]No token entered. Aborted.[/]");
                return false;
            }
            token = token.Trim();
            SmehState.SetGitHubAccessToken(token);
            AnsiConsole.MarkupLine("[green]PAT saved for future runs.[/]");
        }

        await RunDirectXAndVcRedistAsync();

        var releaseUrl = $"https://api.github.com/repos/{repo}/releases/latest";
        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Fetching latest release info...[/]");

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
                    AnsiConsole.MarkupLine($@"To authorize: use a GitHub PAT (env [{SmehTheme.FicsitOrange}]SMEH_GITHUB_PAT[/]) or enter it when prompted.");
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
                    AnsiConsole.MarkupLine("Alternatively, use a direct download URL for the engine.");
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Failed to get release: {response.StatusCode}. Check Repository or try DownloadUrl.[/]");
                }
                response.Dispose();
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            response.Dispose();
            return await DownloadAndInstallFromReleaseJson(authClient, json);
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

        var installDir = Path.Combine(CleanupService.TempRoot, "CssUnrealEngine");
        Directory.CreateDirectory(installDir);

        var ghHeaders = new Dictionary<string, string> { ["Accept"] = "application/octet-stream" };
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p));

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Downloading installer (.exe)...[/]");
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

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]All files ready. Running installer...[/]");
        if (!PromptInstallPath())
            return false;
        var result = await _processRunner.RunAsync(exePath, GetInstallerArgs(_options.InstallPath), installDir, waitForExit: true);
        if (result.ExitCode != 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
        else
        {
            AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
            var installerFiles = new List<string> { exePath };
            installerFiles.AddRange(binAssets.Select(b => Path.Combine(installDir, b.Name)));
            if (!SmehState.RunAllUnattended)
                OfferToDeleteInstallerFiles(installerFiles);
        }
        return result.ExitCode == 0;
    }

    private async Task<bool> RunLegacyDownloadAsync(string downloadUrl)
    {
        var tempDir = Path.Combine(CleanupService.TempRoot, "CssUnrealEngine");
        Directory.CreateDirectory(tempDir);

        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrEmpty(fileName))
            fileName = "css-unreal-engine.zip";
        var destPath = Path.Combine(tempDir, fileName);

        AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Downloading CSS Unreal Engine...[/]");
        var progress = new Progress<DownloadProgress>(p => ConsoleProgressBar.Report(p, "CSS UE"));
        await _downloadHelper.DownloadFileAsync(downloadUrl, destPath, progress);
        ConsoleProgressBar.Clear();
        AnsiConsole.MarkupLine("[green]Download complete.[/]");

        var isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (isZip)
        {
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Extracting...[/]");
            System.IO.Compression.ZipFile.ExtractToDirectory(destPath, extractDir, overwriteFiles: true);
            AnsiConsole.MarkupLineInterpolated($"[dim]Extracted to: {Markup.Escape(extractDir)}[/]");
            return false; // User may need to run installer manually
        }
        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Running installer...[/]");
            if (!PromptInstallPath())
                return false;
            var result = await _processRunner.RunAsync(destPath, GetInstallerArgs(_options.InstallPath), tempDir, waitForExit: true);
            if (result.ExitCode != 0)
                AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
            else
            {
                AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
                if (!SmehState.RunAllUnattended)
                    OfferToDeleteInstallerFiles(new[] { destPath });
            }
            return result.ExitCode == 0;
        }
        AnsiConsole.MarkupLineInterpolated($"[dim]Downloaded to: {Markup.Escape(destPath)}[/]");
        AnsiConsole.MarkupLine("[yellow]Unknown file type. Extract or run manually as needed.[/]");
        return false;
    }

    /// <summary>Prompts for install location (default or custom). Sets <see cref="CssUnrealEngineOptions.InstallPath"/> and returns true if a path was chosen, false if cancelled.</summary>
    private bool PromptInstallPath()
    {
        if (SmehState.RunAllUnattended && !string.IsNullOrWhiteSpace(_options.InstallPath))
            return true;
        AnsiConsole.MarkupLineInterpolated($"[dim]Default install location: [white]{Markup.Escape(DefaultInstallPath)}[/][/]");
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Use this location or choose a custom path?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Use default", "Custom path"));
        if (choice == "Use default")
        {
            _options.InstallPath = DefaultInstallPath;
            return true;
        }
        var customPath = AnsiConsole.Prompt(new TextPrompt<string>("Enter custom install path (or press Enter to cancel):")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(customPath))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
            return false;
        }
        customPath = customPath!.Trim();
        if (!Directory.Exists(Path.GetPathRoot(customPath) ?? ""))
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]Drive or root not found. Installation may create the folder: {Markup.Escape(customPath)}[/]");
        }
        _options.InstallPath = customPath;
        return true;
    }

    private static string GetInstallerArgs(string installPath)
    {
        var args = UnrealEngineInstallerArgs;
        if (!string.Equals(installPath.Trim(), DefaultInstallPath, StringComparison.OrdinalIgnoreCase))
            args += $" /DIR=\"{installPath.Trim()}\"";
        return args;
    }

    /// <summary>Asks if the user wants to delete Unreal Engine installer files (exe + .bin). Uses LastUnrealEngineInstallerFolder when set (run-all Manual path); otherwise skips. Used at end of run-all.</summary>
    public static void OfferToDeleteEngineInstallerFiles()
    {
        var folder = SmehState.LastUnrealEngineInstallerFolder?.Trim();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            SmehState.LastUnrealEngineInstallerFolder = null;
            return;
        }
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title($"Delete Unreal Engine installer files from {Markup.Escape(folder)}?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        SmehState.LastUnrealEngineInstallerFolder = null;
        if (choice != "Yes")
            return;
        var filePaths = new[] { Path.Combine(folder, ManualInstallExe), Path.Combine(folder, ManualInstallBin1), Path.Combine(folder, ManualInstallBin2) };
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    AnsiConsole.MarkupLineInterpolated($"[dim]Deleted {Markup.Escape(Path.GetFileName(path))}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Could not delete {Markup.Escape(path)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }

    /// <summary>Offers to delete the given installer files. Deletes only if user chooses Yes.</summary>
    private static void OfferToDeleteInstallerFiles(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return;
        var choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Delete installer files?")
            .HighlightStyle(SmehTheme.AccentStyle)
            .AddChoices("Yes", "No"));
        if (choice != "Yes")
            return;
        foreach (var path in filePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    AnsiConsole.MarkupLineInterpolated($"[dim]Deleted {Markup.Escape(Path.GetFileName(path))}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]Could not delete {Markup.Escape(path)}: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }

    /// <summary>Returns true if folder contains the exact exe and both .bin files from the manual install set.</summary>
    private static bool HasManualInstallFiles(string folder, out string? exePath)
    {
        exePath = null;
        var exe = Path.Combine(folder, ManualInstallExe);
        var bin1 = Path.Combine(folder, ManualInstallBin1);
        var bin2 = Path.Combine(folder, ManualInstallBin2);
        if (!File.Exists(exe) || !File.Exists(bin1) || !File.Exists(bin2))
            return false;
        exePath = exe;
        return true;
    }

    /// <summary>Gets the folder containing the manually downloaded installer: tries Downloads first, then prompts.</summary>
    private static string? GetManualInstallFolder()
    {
        var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloadsPath) && HasManualInstallFiles(downloadsPath, out _))
        {
            var useDownloads = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title($"Found installer in Downloads folder. Use [{SmehTheme.FicsitOrange}]{Markup.Escape(downloadsPath)}[/]?")
                .HighlightStyle(SmehTheme.AccentStyle)
                .AddChoices("Yes", "No, choose another folder"));
            if (useDownloads == "Yes")
                return downloadsPath;
        }

        var folder = AnsiConsole.Prompt(new TextPrompt<string>("Files not found in Downloads. Enter the folder path where you saved the UnrealEngine-CSS-Editor-Win64 files:")
            .AllowEmpty());
        if (string.IsNullOrWhiteSpace(folder))
        {
            AnsiConsole.MarkupLine($"[{SmehTheme.FicsitOrange}]Cancelled.[/]");
            return null;
        }
        folder = folder.Trim();
        if (!Directory.Exists(folder))
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Folder not found: {Markup.Escape(folder)}[/]");
            return null;
        }
        if (!HasManualInstallFiles(folder, out _))
        {
            AnsiConsole.MarkupLine($"[red]Folder must contain exactly: [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64.exe[/], [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64-1.bin[/], and [{SmehTheme.FicsitOrange}]UnrealEngine-CSS-Editor-Win64-2.bin[/].[/]");
            return null;
        }
        return folder;
    }

    private async Task<bool> RunInstallerFromFolderAsync(string folder)
    {
        if (SmehState.RunAllUnattended)
            SmehState.LastUnrealEngineInstallerFolder = folder;
        if (!HasManualInstallFiles(folder, out var exePath) || exePath == null)
        {
            AnsiConsole.MarkupLine("[red]Installer files not found.[/]");
            return false;
        }
        AnsiConsole.MarkupLineInterpolated($"[dim]Running installer: {Markup.Escape(ManualInstallExe)}[/]");
        if (!PromptInstallPath())
            return false;
        var result = await _processRunner.RunAsync(exePath, GetInstallerArgs(_options.InstallPath), folder, waitForExit: true);
        if (result.ExitCode != 0)
            AnsiConsole.MarkupLineInterpolated($"[yellow]Installer exited with code {result.ExitCode}.[/]");
        else
        {
            AnsiConsole.MarkupLine("[green]Installation finished successfully.[/]");
            if (!SmehState.RunAllUnattended)
                OfferToDeleteInstallerFiles(new[]
                {
                    Path.Combine(folder, ManualInstallExe),
                    Path.Combine(folder, ManualInstallBin1),
                    Path.Combine(folder, ManualInstallBin2)
                });
        }
        return result.ExitCode == 0;
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
