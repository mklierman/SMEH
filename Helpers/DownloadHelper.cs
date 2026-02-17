using System.Net.Http;

namespace SMEH.Helpers;

public class DownloadHelper
{
    private static readonly HttpClient DefaultClient = new();

    public async Task DownloadFileAsync(string url, string destPath, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await DownloadFileAsync(url, destPath, progress, cancellationToken, null, null);
    }

    /// <summary>Download using an optional HttpClient and optional request headers (such as Authorization and Accept for GitHub API).</summary>
    public async Task DownloadFileAsync(string url, string destPath, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken, HttpClient? client, IReadOnlyDictionary<string, string>? requestHeaders = null)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var httpClient = client ?? DefaultClient;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (requestHeaders != null)
        {
            foreach (var (key, value) in requestHeaders)
                request.Headers.TryAddWithoutValidation(key, value);
        }
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var totalBytesRead = 0L;
        var buffer = new byte[81920];

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);

        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(new DownloadProgress(totalBytesRead, totalBytes));
        }
    }
}

public record DownloadProgress(long BytesRead, long? TotalBytes);
