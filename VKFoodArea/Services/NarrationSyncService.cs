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
        string language,
        string mode,
        string triggerSource = "manual",
        CancellationToken ct = default)
    {
        try
        {
            var payload = new NarrationHistoryPushDto
            {
                PoiId = poiId,
                PoiName = poiName,
                Language = language,
                TriggerSource = triggerSource,
                Mode = mode.ToLowerInvariant(),
                PlayedAt = DateTime.UtcNow
            };

            var url = $"{_apiBaseUrlService.BaseUrl}api/narration-histories";
            await _httpClient.PostAsJsonAsync(url, payload, ct);
        }
        catch
        {
            // Web lỗi thì bỏ qua, app vẫn dùng local log
        }
    }
}