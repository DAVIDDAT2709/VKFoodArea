using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class PoiAudioCacheService
{
    private static readonly TimeSpan AudioDownloadTimeout = TimeSpan.FromSeconds(90);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public PoiAudioCacheService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        ApiBaseUrlService apiBaseUrlService)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _apiBaseUrlService = apiBaseUrlService;
    }

    public async Task<PoiAudioCacheResult> DownloadAllAsync(CancellationToken ct = default)
    {
        var pois = await _db.Pois
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(ct);

        if (pois.Count == 0)
            return PoiAudioCacheResult.Empty;

        var cacheDirectory = GetAudioCacheDirectory();
        Directory.CreateDirectory(cacheDirectory);

        var httpClient = _httpClientFactory.CreateClient(AppRemoteHttpClientNames.Primary);
        httpClient.Timeout = AudioDownloadTimeout;

        var downloadedCount = 0;
        var reusedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var hasChanges = false;

        foreach (var poi in pois)
        {
            var vi = await CacheAudioAsync(httpClient, cacheDirectory, poi.Id, "vi", poi.AudioFileVi, ct);
            var audioFileVi = poi.AudioFileVi;
            ApplyCacheResult(vi, ref audioFileVi, ref downloadedCount, ref reusedCount, ref skippedCount, ref failedCount, ref hasChanges);
            poi.AudioFileVi = audioFileVi;

            var en = await CacheAudioAsync(httpClient, cacheDirectory, poi.Id, "en", poi.AudioFileEn, ct);
            var audioFileEn = poi.AudioFileEn;
            ApplyCacheResult(en, ref audioFileEn, ref downloadedCount, ref reusedCount, ref skippedCount, ref failedCount, ref hasChanges);
            poi.AudioFileEn = audioFileEn;

            var ja = await CacheAudioAsync(httpClient, cacheDirectory, poi.Id, "ja", poi.AudioFileJa, ct);
            var audioFileJa = poi.AudioFileJa;
            ApplyCacheResult(ja, ref audioFileJa, ref downloadedCount, ref reusedCount, ref skippedCount, ref failedCount, ref hasChanges);
            poi.AudioFileJa = audioFileJa;
        }

        if (hasChanges)
            await _db.SaveChangesAsync(ct);

        return new PoiAudioCacheResult(downloadedCount, reusedCount, skippedCount, failedCount);
    }

    public async Task UseCachedAudioForAsync(QrResolveResult resolved, CancellationToken ct = default)
    {
        var localPois = await _db.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        if (resolved.Poi is not null)
            ApplyCachedAudio(localPois, resolved.Poi);

        if (resolved.Tour is null)
            return;

        foreach (var stop in resolved.Tour.Stops)
            ApplyCachedAudio(localPois, stop.Poi);
    }

    private async Task<AudioCacheAssetResult> CacheAudioAsync(
        HttpClient httpClient,
        string cacheDirectory,
        int poiId,
        string language,
        string audioPath,
        CancellationToken ct)
    {
        var remoteUrl = ResolveDownloadUrl(audioPath);
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return AudioCacheAssetResult.Skipped;

        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var remoteUri))
            return AudioCacheAssetResult.Skipped;

        var localPath = BuildLocalAudioPath(cacheDirectory, poiId, language, remoteUri);
        if (File.Exists(localPath) && new FileInfo(localPath).Length > 0)
            return AudioCacheAssetResult.Reused(localPath);

        var tempPath = $"{localPath}.tmp";

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using var response = await httpClient.GetAsync(
                remoteUri,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            if (!response.IsSuccessStatusCode)
                return AudioCacheAssetResult.Failed;

            await using (var source = await response.Content.ReadAsStreamAsync(ct))
            await using (var target = File.Create(tempPath))
            {
                await source.CopyToAsync(target, ct);
            }

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                return AudioCacheAssetResult.Failed;

            File.Move(tempPath, localPath, overwrite: true);
            return AudioCacheAssetResult.Downloaded(localPath);
        }
        catch
        {
            TryDeleteFile(tempPath);
            return AudioCacheAssetResult.Failed;
        }
    }

    private string ResolveDownloadUrl(string? audioPath)
    {
        var normalized = (audioPath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
        {
            return IsHttpUri(absoluteUri)
                ? absoluteUri.ToString()
                : string.Empty;
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return _apiBaseUrlService.ResolveRemoteUrl(normalized);
        }

        if (Path.IsPathRooted(normalized))
            return string.Empty;

        return string.Empty;
    }

    private static void ApplyCacheResult(
        AudioCacheAssetResult result,
        ref string audioPath,
        ref int downloadedCount,
        ref int reusedCount,
        ref int skippedCount,
        ref int failedCount,
        ref bool hasChanges)
    {
        switch (result.Status)
        {
            case AudioCacheAssetStatus.Downloaded:
                downloadedCount++;
                SetLocalAudioPath(result.LocalPath, ref audioPath, ref hasChanges);
                break;
            case AudioCacheAssetStatus.Reused:
                reusedCount++;
                SetLocalAudioPath(result.LocalPath, ref audioPath, ref hasChanges);
                break;
            case AudioCacheAssetStatus.Failed:
                failedCount++;
                break;
            default:
                skippedCount++;
                break;
        }
    }

    private static void SetLocalAudioPath(string localPath, ref string audioPath, ref bool hasChanges)
    {
        if (string.IsNullOrWhiteSpace(localPath) ||
            string.Equals(audioPath, localPath, StringComparison.Ordinal))
        {
            return;
        }

        audioPath = localPath;
        hasChanges = true;
    }

    private static void ApplyCachedAudio(IReadOnlyCollection<Poi> localPois, Poi poi)
    {
        var cached = PoiReferenceMatcher.FindMatch(localPois, poi);
        if (cached is null)
            return;

        poi.AudioFileVi = PreferCachedAudioPath(cached.AudioFileVi, poi.AudioFileVi);
        poi.AudioFileEn = PreferCachedAudioPath(cached.AudioFileEn, poi.AudioFileEn);
        poi.AudioFileJa = PreferCachedAudioPath(cached.AudioFileJa, poi.AudioFileJa);
    }

    private static string PreferCachedAudioPath(string cachedPath, string fallbackPath)
        => IsUsableLocalAudioPath(cachedPath)
            ? cachedPath
            : fallbackPath;

    private static bool IsUsableLocalAudioPath(string? audioPath)
    {
        var normalized = (audioPath ?? string.Empty).Trim();
        return Path.IsPathRooted(normalized) && File.Exists(normalized);
    }

    private static string BuildLocalAudioPath(string cacheDirectory, int poiId, string language, Uri remoteUri)
    {
        var extension = Path.GetExtension(remoteUri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 12)
            extension = ".mp3";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(remoteUri.ToString())))
            .ToLowerInvariant();
        var fileName = $"poi_{poiId}_{language}_{hash[..16]}{extension.ToLowerInvariant()}";
        return Path.Combine(cacheDirectory, fileName);
    }

    private static string GetAudioCacheDirectory()
        => Path.Combine(FileSystem.AppDataDirectory, "poi-audio-cache");

    private static bool IsHttpUri(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}

public sealed record PoiAudioCacheResult(
    int DownloadedCount,
    int ReusedCount,
    int SkippedCount,
    int FailedCount)
{
    public static PoiAudioCacheResult Empty { get; } = new(0, 0, 0, 0);

    public int CachedCount => DownloadedCount + ReusedCount;
}

internal enum AudioCacheAssetStatus
{
    Skipped,
    Downloaded,
    Reused,
    Failed
}

internal sealed record AudioCacheAssetResult(
    AudioCacheAssetStatus Status,
    string LocalPath)
{
    public static AudioCacheAssetResult Skipped { get; } = new(AudioCacheAssetStatus.Skipped, string.Empty);
    public static AudioCacheAssetResult Failed { get; } = new(AudioCacheAssetStatus.Failed, string.Empty);

    public static AudioCacheAssetResult Downloaded(string localPath)
        => new(AudioCacheAssetStatus.Downloaded, localPath);

    public static AudioCacheAssetResult Reused(string localPath)
        => new(AudioCacheAssetStatus.Reused, localPath);
}
