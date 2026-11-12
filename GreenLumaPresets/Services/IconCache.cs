using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace GreenLumaPresets.Services;

public class IconCache : IIconCache
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly HttpClient client;
    private readonly string directory;
    private readonly ILogger<IconCache>? logger;

    // The same icon is often shown by several rows: they share one download.
    private readonly Dictionary<string, Task<string?>> downloads = [];

    public IconCache(ILogger<IconCache> logger) : this(SharedClient, Path.Combine(AppPaths.CacheDirectory, "icons"), logger)
    {
    }

    public IconCache(HttpClient client, string directory, ILogger<IconCache>? logger = null)
    {
        this.client = client;
        this.directory = directory;
        this.logger = logger;
    }

    public Task<string?> GetAsync(string url)
    {
        lock (downloads)
        {
            if (!downloads.TryGetValue(url, out var download) || download.IsCompleted && download.Result == null)
            {
                // A failed download is retried on the next request.
                download = DownloadAsync(url);
                downloads[url] = download;
            }
            return download;
        }
    }

    private async Task<string?> DownloadAsync(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..32].ToLowerInvariant();
        var path = Path.Combine(directory, $"{hash}.jpg");
        if (File.Exists(path)) return path;

        try
        {
            var bytes = await client.GetByteArrayAsync(url);
            Directory.CreateDirectory(directory);

            // Written aside then moved: a partial file is never taken for a cached icon.
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes);
            File.Move(temporaryPath, path, overwrite: true);
            return path;
        }
        catch (Exception err) when (err is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(err, "Couldn't download the icon {Url}", url);
            return null;
        }
    }
}
