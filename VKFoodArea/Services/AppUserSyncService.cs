using System.Diagnostics;
using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AppUserSyncService
{
    private readonly HttpClient _httpClient;
    private readonly AppSyncOutboxService _outboxService;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public AppUserSyncService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AppSyncOutboxService outboxService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
        _outboxService = outboxService;
    }

    public async Task SyncAsync(AppUser user, string? userKey, CancellationToken ct = default)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
            return;

        try
        {
            var payload = new AppUserSyncDto
            {
                UserKey = normalizedUserKey,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                NarrationLanguage = AppLanguageService.NormalizeLanguage(user.NarrationLanguage),
                NarrationPlaybackMode = SoundSettingsService.NormalizePlaybackMode(user.NarrationPlaybackMode),
                Role = AppUserRoleNames.Normalize(user.Role),
                IsActive = user.IsActive
            };

            await _outboxService.EnqueueAsync("app-user-sync", "api/app-users/sync", payload, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App user sync failed: {ex}");
        }
    }

    public async Task<AppUserStatusDto?> GetStatusAsync(string? userKey, CancellationToken ct = default)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
            return null;

        try
        {
            if (!_apiBaseUrlService.TryBuildApiUrl(
                    $"api/app-users/status?userKey={Uri.EscapeDataString(normalizedUserKey)}",
                    out var url))
            {
                return null;
            }

            return await _httpClient.GetFromJsonAsync<AppUserStatusDto>(url, ct);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App user status check failed: {ex}");
            return null;
        }
    }

    private static string NormalizeUserKey(string? userKey)
        => (userKey ?? string.Empty).Trim().ToLowerInvariant();
}
