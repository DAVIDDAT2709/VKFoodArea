using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;
#if ANDROID
using Android.Util;
#endif

namespace VKFoodArea.Services;

public class PoiSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly AppDbContext _db;

    public PoiSyncService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AppDbContext db)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
        _db = db;
    }

    public static event EventHandler<PoiSyncCompletedEventArgs>? SyncCompleted;

    public async Task<PoiSyncResult> SyncPoisAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{_apiBaseUrlService.BaseUrl}api/pois";
            WritePlatformLog($"POI sync started: {url}");
            var remotePois = await _httpClient.GetFromJsonAsync<List<RemotePoiDto>>(url, ct);

            if (remotePois is null)
            {
                await WriteDebugTraceAsync("POI sync failed: Web API returned null.");
                WritePlatformLog("POI sync failed: Web API returned null.");
                return PoiSyncResult.Failed("Web API khong tra ve du lieu POI.");
            }

            if (remotePois.Count == 0)
            {
                await WriteDebugTraceAsync("POI sync skipped: Web API returned 0 POIs.");
                WritePlatformLog("POI sync skipped: Web API returned 0 POIs.");
                return PoiSyncResult.Failed("Web API dang tra ve 0 POI, bo qua de tranh xoa du lieu local.");
            }

            var localPois = await _db.Pois.ToListAsync(ct);
            var localByQr = localPois
                .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
                .GroupBy(x => NormalizeQrCode(x.QrCode))
                .ToDictionary(x => x.Key, x => x.First());
            var localByIdentity = localPois
                .Select(x => new
                {
                    Poi = x,
                    Key = BuildIdentityKey(x.Name, x.Address)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First().Poi, StringComparer.Ordinal);
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            foreach (var dto in remotePois)
            {
                var local = FindExistingPoi(dto, localByQr, localByIdentity);
                if (local is null)
                {
                    local = new Poi();
                    _db.Pois.Add(local);
                }

                ApplyRemotePoi(local, dto);

                var normalizedQr = NormalizeQrCode(local.QrCode);
                if (!string.IsNullOrWhiteSpace(normalizedQr))
                    localByQr[normalizedQr] = local;

                var identityKey = BuildIdentityKey(local.Name, local.Address);
                if (!string.IsNullOrWhiteSpace(identityKey))
                    localByIdentity[identityKey] = local;
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _db.ChangeTracker.Clear();

            var result = PoiSyncResult.Succeeded(remotePois.Count);
            await WriteDebugTraceAsync($"POI sync success: url={url} remote={remotePois.Count}");
            WritePlatformLog($"POI sync success: url={url} remote={remotePois.Count}");
            NotifySyncCompleted(result);
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"POI sync failed: {ex}");
            await WriteDebugTraceAsync($"POI sync failed: {ex}");
            WritePlatformLog($"POI sync failed: {ex}");
            var result = PoiSyncResult.Failed(ex.Message);
            NotifySyncCompleted(result);
            return result;
        }
    }

    private static string NormalizeQrCode(string? qrCode)
        => (qrCode ?? string.Empty).Trim().ToLowerInvariant();

    private static Poi? FindExistingPoi(
        RemotePoiDto dto,
        IReadOnlyDictionary<string, Poi> localByQr,
        IReadOnlyDictionary<string, Poi> localByIdentity)
    {
        var normalizedQr = NormalizeQrCode(dto.QrCode);
        if (!string.IsNullOrWhiteSpace(normalizedQr) &&
            localByQr.TryGetValue(normalizedQr, out var byQr))
        {
            return byQr;
        }

        var identityKey = BuildIdentityKey(dto.Name, dto.Address);
        if (!string.IsNullOrWhiteSpace(identityKey) &&
            localByIdentity.TryGetValue(identityKey, out var byIdentity))
        {
            return byIdentity;
        }

        return null;
    }

    private void ApplyRemotePoi(Poi local, RemotePoiDto dto)
    {
        local.Name = dto.Name;
        local.Address = dto.Address;
        local.PhoneNumber = dto.PhoneNumber;
        local.Latitude = dto.Latitude;
        local.Longitude = dto.Longitude;
        local.RadiusMeters = dto.RadiusMeters;
        local.Priority = dto.Priority;
        local.Description = dto.Description;
        local.TtsScriptVi = dto.TtsScriptVi;
        local.TtsScriptEn = dto.TtsScriptEn;
        local.TtsScriptZh = dto.TtsScriptZh;
        local.TtsScriptJa = dto.TtsScriptJa;
        local.TtsScriptDe = dto.TtsScriptDe;
        local.AudioFileVi = dto.AudioFileVi;
        local.AudioFileEn = dto.AudioFileEn;
        local.AudioFileJa = dto.AudioFileJa;
        local.ImageUrl = _apiBaseUrlService.ResolveImageUrl(dto.ImageUrl);
        local.QrCode = dto.QrCode;
        local.IsActive = dto.IsActive;
        local.MapUrl = CreateMapUrl(dto.Latitude, dto.Longitude);
    }

    private static string BuildIdentityKey(string? name, string? address)
    {
        var normalizedName = (name ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedAddress = (address ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedName) &&
            string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return string.Empty;
        }

        return $"{normalizedName}|{normalizedAddress}";
    }

    private static string CreateMapUrl(double latitude, double longitude)
        => $"https://maps.google.com/?q={latitude},{longitude}";

    public sealed record PoiSyncResult(bool Success, int RemoteCount, string? ErrorMessage)
    {
        public static PoiSyncResult Succeeded(int remoteCount) => new(true, remoteCount, null);

        public static PoiSyncResult Failed(string? errorMessage) => new(false, 0, errorMessage);
    }

    public sealed class PoiSyncCompletedEventArgs : EventArgs
    {
        public PoiSyncCompletedEventArgs(PoiSyncResult result)
        {
            Result = result;
        }

        public PoiSyncResult Result { get; }
    }

    private static Task WriteDebugTraceAsync(string message)
    {
        var tracePath = Path.Combine(FileSystem.AppDataDirectory, "poi-sync-status.txt");
        var payload = $"{DateTimeOffset.Now:O}{Environment.NewLine}{message}{Environment.NewLine}";
        return File.WriteAllTextAsync(tracePath, payload);
    }

    private static void WritePlatformLog(string message)
    {
        Debug.WriteLine(message);
#if ANDROID
        Log.Info("VKFoodArea", message);
#endif
    }

    private static void NotifySyncCompleted(PoiSyncResult result)
    {
        SyncCompleted?.Invoke(null, new PoiSyncCompletedEventArgs(result));
    }
}
