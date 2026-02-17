using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SMEH.Helpers;

/// <summary>GitHub OAuth 2.0 device flow</summary>
public static class GitHubOAuthHelper
{
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string AccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string Scope = "repo read:org";

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { UserAgent = { new ProductInfoHeaderValue("SMEH", "1.0") } }
    };

    /// <summary>Runs the device flow: shows user the URL and code, polls until they authorize, returns the access token or null.</summary>
    public static async Task<string?> RunDeviceFlowAsync(string clientId, CancellationToken cancellationToken = default)
    {
        // 1. Request device code
        var codePayload = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = Scope
        };

        using var codeRequest = new HttpRequestMessage(HttpMethod.Post, DeviceCodeUrl);
        codeRequest.Content = new StringContent(JsonSerializer.Serialize(codePayload), Encoding.UTF8, "application/json");
        codeRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var codeResponse = await HttpClient.SendAsync(codeRequest, cancellationToken);
        codeResponse.EnsureSuccessStatusCode();
        var codeBody = await codeResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!TryParseDeviceCodeResponse(codeBody, out var deviceCode, out var userCode, out var verificationUri, out var interval, out var expiresIn))
        {
            Console.WriteLine($"Unexpected response from GitHub (first 200 chars): {(codeBody.Length > 200 ? codeBody[..200] : codeBody)}");
            return null;
        }

        if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode))
        {
            Console.WriteLine("Invalid response from GitHub.");
            return null;
        }

        TryCopyToClipboard(userCode!);

        Console.WriteLine("To authorize this app to access the private repository:");
        Console.WriteLine($"  1. Open in your browser: {verificationUri}");
        Console.WriteLine("     (Ctrl+Click the URL above to open it in your browser.)");
        Console.WriteLine($"  2. Enter this code: {userCode} (copied to clipboard)");
        Console.WriteLine("  3. Sign in with GitHub and authorize the app.");
        Console.WriteLine();
        Console.WriteLine("Waiting for you to authorize...");

        // 2. Poll for access token
        var tokenPayload = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["device_code"] = deviceCode!,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
        };

        var deadline = DateTime.UtcNow.AddSeconds(expiresIn);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, AccessTokenUrl);
            tokenRequest.Content = new StringContent(JsonSerializer.Serialize(tokenPayload), Encoding.UTF8, "application/json");
            tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var tokenResponse = await HttpClient.SendAsync(tokenRequest, cancellationToken);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!TryParseTokenResponse(tokenBody, out var accessToken, out var error))
            {
                Console.WriteLine($"Unexpected token response (first 200 chars): {(tokenBody.Length > 200 ? tokenBody[..200] : tokenBody)}");
                return null;
            }
            if (accessToken != null)
            {
                return accessToken;
            }
            if (error == "authorization_pending" || error == "slow_down")
            {
                continue;
            }
            if (error == "expired_token")
            {
                Console.WriteLine("The authorization code expired. Please run this option again.");
                return null;
            }
            if (error == "access_denied")
            {
                Console.WriteLine("Authorization was denied.");
                return null;
            }
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"Authorization error: {error}");
                return null;
            }
        }

        Console.WriteLine("Authorization timed out. Please run this option again.");
        return null;
    }

    /// <summary>Tries to parse the device code response.</summary>
    /// <returns>True if the response is parsed successfully, false otherwise.</returns>
    private static bool TryParseDeviceCodeResponse(string body, out string? deviceCode, out string? userCode, out string? verificationUri, out int interval, out int expiresIn)
    {
        deviceCode = null;
        userCode = null;
        verificationUri = "https://github.com/login/device";
        interval = 5;
        expiresIn = 900;

        if (string.IsNullOrWhiteSpace(body)) return false;

        // Try JSON first
        if (body.TrimStart().StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                deviceCode = root.TryGetProperty("device_code", out var dc) ? dc.GetString() : null;
                userCode = root.TryGetProperty("user_code", out var uc) ? uc.GetString() : null;
                if (root.TryGetProperty("verification_uri", out var v)) verificationUri = v.GetString();
                if (root.TryGetProperty("interval", out var i)) interval = i.GetInt32();
                if (root.TryGetProperty("expires_in", out var e)) expiresIn = e.GetInt32();
                return !string.IsNullOrEmpty(deviceCode) && !string.IsNullOrEmpty(userCode);
            }
            catch
            {
                return false;
            }
        }

        // Fallback: form-urlencoded (e.g. device_code=xxx&user_code=yyy)
        var parsed = ParseFormUrlEncoded(body);
        if (parsed.TryGetValue("device_code", out var dcVal)) deviceCode = dcVal;
        if (parsed.TryGetValue("user_code", out var ucVal)) userCode = ucVal;
        if (parsed.TryGetValue("verification_uri", out var vVal)) verificationUri = vVal;
        if (parsed.TryGetValue("interval", out var iVal) && int.TryParse(iVal, out var iv)) interval = iv;
        if (parsed.TryGetValue("expires_in", out var eVal) && int.TryParse(eVal, out var ev)) expiresIn = ev;
        return !string.IsNullOrEmpty(deviceCode) && !string.IsNullOrEmpty(userCode);
    }

    private static bool TryParseTokenResponse(string body, out string? accessToken, out string? error)
    {
        accessToken = null;
        error = null;
        if (string.IsNullOrWhiteSpace(body)) return true;

        if (body.TrimStart().StartsWith("{"))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("access_token", out var at)) accessToken = at.GetString();
                if (root.TryGetProperty("error", out var err)) error = err.GetString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        var parsed = ParseFormUrlEncoded(body);
        parsed.TryGetValue("access_token", out accessToken);
        parsed.TryGetValue("error", out error);
        return true;
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in body.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(pair[..idx].Trim());
            var value = Uri.UnescapeDataString(pair[(idx + 1)..].Trim());
            result[key] = value;
        }
        return result;
    }

    private static void TryCopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        text = text.Trim();
        try
        {
            // PowerShell avoids the trailing newline that "echo X | clip" adds
            var escaped = text.Replace("'", "''");
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Set-Clipboard -Value '{escaped}'\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process?.WaitForExit(2000);
        }
        catch
        {
            // User can type the code manually
        }
    }
}
