using System.Diagnostics;
using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class NarrationSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public NarrationSyncService(HttpClient httpClient, ApiBaseUrlService apiBaseUrlService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
    }

    public async Task PushHistoryAsync(
        int poiId,
        string poiName,
        string qrCode,
        string language,
        string mode,
        string triggerSource = "manual",
        CancellationToken ct = default)
    {
        try
        {
            var payload = new NarrationHistoryPushDto
{
    // Không đẩy local PoiId lên web để tránh map nhầm quán khi Id local khác Id web.
    PoiId = 0,
    PoiName = poiName,
    QrCode = qrCode,
    Language = language,
    TriggerSource = NormalizeTriggerSource(triggerSource),
    Mode = mode.ToLowerInvariant(),
    PlayedAt = DateTime.UtcNow
};

            var url = $"{_apiBaseUrlService.BaseUrl}api/narration-histories";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Narration history sync failed: {ex}");
            // Web lỗi thì bỏ qua, app vẫn dùng local log
        }
    }

    public async Task<IReadOnlyList<NarrationHistoryRemoteItem>> GetRecentHistoryAsync(
        string? source = null,
        int top = 100,
        CancellationToken ct = default)
    {
        var normalizedTop = Math.Clamp(top, 1, 200);
        var normalizedSource = NormalizeTriggerSource(source);
        var url = $"{_apiBaseUrlService.BaseUrl}api/narration-histories?top={normalizedTop}";

        if (!string.IsNullOrWhiteSpace(normalizedSource))
            url += $"&source={Uri.EscapeDataString(normalizedSource)}";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<NarrationHistoryRemoteItem>>(cancellationToken: ct)
               ?? [];
    }

    private static string NormalizeTriggerSource(string? triggerSource)
    {
        var normalized = (triggerSource ?? "manual").Trim().ToLowerInvariant();

        return normalized switch
        {
            "auto" => "gps",
            "gps" => "gps",
            "qr" => "qr",
            _ => "manual"
        };
    }
}
