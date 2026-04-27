using System.Diagnostics;
using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class NarrationSyncService
{
    private readonly HttpClient _httpClient;
    private readonly AppSyncOutboxService _outboxService;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public NarrationSyncService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AppSyncOutboxService outboxService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
        _outboxService = outboxService;
    }

    public async Task PushHistoryAsync(
        int poiId,
        string poiName,
        string qrCode,
        string language,
        string mode,
        string? userKey,
        string triggerSource = "manual",
        int? tourId = null,
        string? tourName = null,
        DateTime? playedAt = null,
        int? durationSeconds = null,
        double? latitude = null,
        double? longitude = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new NarrationHistoryPushDto
            {
                // Không đẩy local PoiId lên web để tránh map nhầm quán khi Id local khác Id web.
                PoiId = 0,
                PoiName = poiName,
                TourId = tourId.HasValue && tourId.Value > 0 ? tourId.Value : null,
                TourName = (tourName ?? string.Empty).Trim(),
                QrCode = qrCode,
                UserKey = NormalizeUserKey(userKey),
                Language = language,
                TriggerSource = NormalizeTriggerSource(triggerSource),
                Mode = mode.ToLowerInvariant(),
                PlayedAt = playedAt ?? DateTime.UtcNow,
                DurationSeconds = durationSeconds,
                Latitude = latitude,
                Longitude = longitude
            };

            await _outboxService.EnqueueAsync("narration-history", "api/narration-histories", payload, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Narration history sync failed: {ex}");
            // Web lỗi thì bỏ qua, app vẫn dùng local log
        }
    }

    public async Task<IReadOnlyList<NarrationHistoryRemoteItem>> GetRecentHistoryAsync(
        string? source = null,
        string? userKey = null,
        int top = 100,
        CancellationToken ct = default)
    {
        var normalizedTop = Math.Clamp(top, 1, 200);
        var normalizedSource = NormalizeTriggerSource(source);
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (!_apiBaseUrlService.TryBuildApiUrl($"api/narration-histories?top={normalizedTop}", out var url))
            return [];

        if (!string.IsNullOrWhiteSpace(normalizedSource))
            url += $"&source={Uri.EscapeDataString(normalizedSource)}";

        if (!string.IsNullOrWhiteSpace(normalizedUserKey))
            url += $"&userKey={Uri.EscapeDataString(normalizedUserKey)}";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<NarrationHistoryRemoteItem>>(cancellationToken: ct)
               ?? [];
    }

    public async Task ClearHistoryAsync(string? userKey, string? source = null, CancellationToken ct = default)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
            return;

        var query = new List<string>
        {
            $"userKey={Uri.EscapeDataString(normalizedUserKey)}"
        };

        var normalizedSource = NormalizeTriggerSource(source);
        if (!string.IsNullOrWhiteSpace(normalizedSource))
            query.Add($"source={Uri.EscapeDataString(normalizedSource)}");

        if (!_apiBaseUrlService.TryBuildApiUrl($"api/narration-histories?{string.Join("&", query)}", out var url))
            return;

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private static string NormalizeTriggerSource(string? triggerSource)
    {
        var normalized = (triggerSource ?? "manual").Trim().ToLowerInvariant();

        return normalized switch
        {
            "auto" => "gps",
            "gps" => "gps",
            "tour" => "tour",
            "qr" => "qr",
            _ => "manual"
        };
    }

    private static string NormalizeUserKey(string? userKey)
        => (userKey ?? string.Empty).Trim().ToLowerInvariant();
}
