using System.Text.Json;
using System.Text.Json.Serialization;
using UAssetAPI.Unversioned;

namespace HeroVoiceFilterEditor.Core.Metadata;

public sealed record UsmapManifestEntry
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("uploaded")]
    public DateTimeOffset Uploaded { get; init; }
}

public sealed record UsmapResult(string? Path, CacheStatus Status, string? RemoteFileName, string? Detail)
{
    public bool IsUsable => Path is not null && File.Exists(Path);
}

/// Keeps the Marvel Rivals usmap current. Mappings.json is a manifest of available usmaps,
/// not a mapping file itself.
public sealed class UsmapService
{
    private readonly string _cacheDirectory;

    public UsmapService(string? cacheDirectory = null) =>
        _cacheDirectory = cacheDirectory ?? AppPaths.UsmapCacheDirectory;

    public string CacheDirectory => _cacheDirectory;

    /// Newest cached usmap by write time, or null when nothing has been downloaded yet.
    public string? NewestCached()
    {
        if (!Directory.Exists(_cacheDirectory))
            return null;

        return Directory.GetFiles(_cacheDirectory, "*.usmap")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public async Task<UsmapManifestEntry?> FetchManifestAsync(CancellationToken cancellationToken = default)
    {
        string json = await RemoteSources.Client.GetStringAsync(RemoteSources.UsmapManifest, cancellationToken);
        UsmapManifestEntry[]? entries = JsonSerializer.Deserialize<UsmapManifestEntry[]>(json);

        return entries?.OrderByDescending(e => e.Uploaded).FirstOrDefault();
    }

    /// Ensures a usable usmap is on disk, downloading only when the cache lacks the newest one.
    public async Task<UsmapResult> EnsureCurrentAsync(bool checkRemote = true, CancellationToken cancellationToken = default)
    {
        string? cached = NewestCached();

        if (!checkRemote)
        {
            return cached is null
                ? new UsmapResult(null, CacheStatus.Unavailable, null, "no cached usmap and remote check disabled")
                : new UsmapResult(cached, CacheStatus.UpToDate, null, "using cache without checking remote");
        }

        UsmapManifestEntry? newest;
        try
        {
            newest = await FetchManifestAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return cached is null
                ? new UsmapResult(null, CacheStatus.Unavailable, null, $"offline and nothing cached: {ex.Message}")
                : new UsmapResult(cached, CacheStatus.Offline, null, $"offline, using cache: {ex.Message}");
        }

        if (newest is null || string.IsNullOrEmpty(newest.Url))
            return new UsmapResult(cached, cached is null ? CacheStatus.Unavailable : CacheStatus.Offline, null, "manifest was empty");

        string target = Path.Combine(_cacheDirectory, newest.FileName);
        if (File.Exists(target))
            return new UsmapResult(target, CacheStatus.UpToDate, newest.FileName, $"cache matches {newest.FileName}");

        try
        {
            AppPaths.Ensure(_cacheDirectory);
            byte[] payload = await RemoteSources.Client.GetByteArrayAsync(newest.Url, cancellationToken);
            await File.WriteAllBytesAsync(target, payload, cancellationToken);
            return new UsmapResult(target, CacheStatus.Downloaded, newest.FileName, $"downloaded {newest.FileName} ({payload.Length} bytes)");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return cached is null
                ? new UsmapResult(null, CacheStatus.Unavailable, newest.FileName, $"download failed, nothing cached: {ex.Message}")
                : new UsmapResult(cached, CacheStatus.UpdateAvailable, newest.FileName, $"download failed, using cache: {ex.Message}");
        }
    }

    public static Usmap Load(string path) => new(path);
}
