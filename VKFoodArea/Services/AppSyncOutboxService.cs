using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class AppSyncOutboxService
{
    private const int BatchSize = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly ILogger<AppSyncOutboxService> _logger;
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public AppSyncOutboxService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ApiBaseUrlService apiBaseUrlService,
        ILogger<AppSyncOutboxService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _apiBaseUrlService = apiBaseUrlService;
        _logger = logger;
    }

    public async Task EnqueueAsync<TPayload>(
        string syncType,
        string relativePath,
        TPayload payload,
        CancellationToken ct = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AppSyncOutboxItems.Add(new AppSyncOutboxItem
        {
            SyncType = NormalizeText(syncType),
            RelativePath = NormalizeText(relativePath).TrimStart('/'),
            PayloadJson = JsonSerializer.Serialize(payload, _jsonSerializerOptions),
            CreatedAt = DateTime.UtcNow,
            NextRetryAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        FlushPendingInBackground();
    }

    public void FlushPendingInBackground()
    {
        _ = Task.Run(() => FlushPendingAsync(CancellationToken.None));
    }

    public async Task FlushPendingAsync(CancellationToken ct = default)
    {
        if (!await _flushGate.WaitAsync(0, ct))
            return;

        try
        {
            if (!_apiBaseUrlService.HasConfiguredBaseUrl)
                return;

            while (!ct.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pendingItems = await db.AppSyncOutboxItems
                    .Where(x => x.DiscardedAt == null && x.NextRetryAt <= DateTime.UtcNow)
                    .OrderBy(x => x.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (pendingItems.Count == 0)
                    return;

                foreach (var item in pendingItems)
                {
                    var outcome = await TryDispatchAsync(item, ct);
                    if (outcome.IsSuccess)
                    {
                        db.AppSyncOutboxItems.Remove(item);
                        continue;
                    }

                    item.AttemptCount += 1;
                    item.LastAttemptAt = DateTime.UtcNow;
                    item.LastError = outcome.ErrorMessage;

                    if (!outcome.ShouldRetry || item.AttemptCount >= AppSyncRetryPolicy.MaxAttempts)
                    {
                        item.DiscardedAt = DateTime.UtcNow;
                        continue;
                    }

                    item.NextRetryAt = DateTime.UtcNow + AppSyncRetryPolicy.GetDelay(item.AttemptCount);
                }

                await db.SaveChangesAsync(ct);

                if (pendingItems.Count < BatchSize)
                    return;
            }
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async Task<DispatchOutcome> TryDispatchAsync(AppSyncOutboxItem item, CancellationToken ct)
    {
        if (!_apiBaseUrlService.TryBuildApiUrl(item.RelativePath, out var url))
        {
            return DispatchOutcome.Transient("Web endpoint is not configured yet.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClientFactory
                .CreateClient(AppRemoteHttpClientNames.Primary)
                .SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return DispatchOutcome.Success();

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var errorMessage = string.IsNullOrWhiteSpace(errorBody)
                ? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}: {errorBody}";

            return AppSyncRetryPolicy.ShouldRetry(response.StatusCode, item.AttemptCount + 1)
                ? DispatchOutcome.Transient(errorMessage, response.StatusCode)
                : DispatchOutcome.Permanent(errorMessage, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App sync dispatch failed for {SyncType}", item.SyncType);
            return DispatchOutcome.Transient(ex.Message);
        }
    }

    private static string NormalizeText(string? value)
        => (value ?? string.Empty).Trim();

    private readonly record struct DispatchOutcome(
        bool IsSuccess,
        bool ShouldRetry,
        string ErrorMessage,
        HttpStatusCode? StatusCode = null)
    {
        public static DispatchOutcome Success() => new(true, false, string.Empty, null);

        public static DispatchOutcome Transient(string errorMessage, HttpStatusCode? statusCode = null)
            => new(false, true, errorMessage, statusCode);

        public static DispatchOutcome Permanent(string errorMessage, HttpStatusCode? statusCode = null)
            => new(false, false, errorMessage, statusCode);
    }
}
