using System.Diagnostics;
using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AppUserSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public AppUserSyncService(HttpClient httpClient, ApiBaseUrlService apiBaseUrlService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
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
                Role = string.IsNullOrWhiteSpace(user.Role) ? "User" : user.Role,
                IsActive = user.IsActive
            };

            var url = $"{_apiBaseUrlService.BaseUrl}api/app-users/sync";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
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
            var url = $"{_apiBaseUrlService.BaseUrl}api/app-users/status?userKey={Uri.EscapeDataString(normalizedUserKey)}";
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
