using System.Diagnostics;
using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class MovementLogSyncService
{
    private readonly AppSyncOutboxService _outboxService;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public MovementLogSyncService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AppSyncOutboxService outboxService)
    {
        _apiBaseUrlService = apiBaseUrlService;
        _outboxService = outboxService;
    }

    public async Task PushAsync(
        double latitude,
        double longitude,
        double? accuracyMeters,
        string? userKey,
        string source = "gps",
        CancellationToken ct = default)
    {
        try
        {
            var payload = new MovementLogPushDto
            {
                UserKey = NormalizeUserKey(userKey),
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracyMeters,
                Source = NormalizeSource(source),
                RecordedAt = DateTime.UtcNow
            };

            await _outboxService.EnqueueAsync("movement-log", "api/movement-logs", payload, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Movement log sync failed: {ex}");
        }
    }

    private static string NormalizeUserKey(string? userKey)
        => (userKey ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeSource(string? source)
    {
        return (source ?? "gps").Trim().ToLowerInvariant() switch
        {
            "background" => "background",
            "foreground" => "foreground",
            "gps" => "gps",
            _ => "gps"
        };
    }
}
