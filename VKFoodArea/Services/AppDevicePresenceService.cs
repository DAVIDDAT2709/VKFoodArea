using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class AppDevicePresenceService : IAsyncDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly AuthService _authService;
    private readonly DeviceIdentityService _deviceIdentityService;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _isForeground;

    public AppDevicePresenceService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AuthService authService,
        DeviceIdentityService deviceIdentityService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
        _authService = authService;
        _deviceIdentityService = deviceIdentityService;
    }

    public async Task SetAppForegroundAsync(bool isForeground)
    {
        await _gate.WaitAsync();
        try
        {
            if (_isForeground == isForeground &&
                ((isForeground && _loopTask is not null) || (!isForeground && _loopTask is null)))
            {
                return;
            }

            _isForeground = isForeground;

            if (isForeground)
            {
                if (_loopTask is null)
                {
                    _loopCts = new CancellationTokenSource();
                    _loopTask = RunHeartbeatLoopAsync(_loopCts.Token);
                }
            }
            else
            {
                _loopCts?.Cancel();
                _loopCts?.Dispose();
                _loopCts = null;
                _loopTask = null;
            }
        }
        finally
        {
            _gate.Release();
        }

        await SendHeartbeatAsync(isForeground);
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(HeartbeatInterval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                await SendHeartbeatAsync(true, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendHeartbeatAsync(bool isOnline, CancellationToken ct = default)
    {
        try
        {
            var currentUser = _authService.CurrentUser;

            var payload = new AppDeviceHeartbeatDto
            {
                DeviceKey = _deviceIdentityService.GetOrCreateDeviceKey(),
                UserKey = _authService.GetCurrentUserSyncKey() ?? string.Empty,
                Username = currentUser?.Username ?? "guest",
                FullName = currentUser?.FullName ?? string.Empty,
                Platform = DeviceInfo.Current.Platform.ToString(),
                DeviceName = $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim(),
                AppVersion = AppInfo.Current.VersionString,
                IsOnline = isOnline
            };

            var url = $"{_apiBaseUrlService.BaseUrl}api/device-presence/heartbeat";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Device heartbeat failed: {ex}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            _loopCts.Cancel();
            _loopCts.Dispose();
        }

        _gate.Dispose();
        await Task.CompletedTask;
    }
}