using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class HomeService : IHomeService
{
    private readonly AppDbContext _context;

    private readonly IAppDevicePresenceService _appDevicePresenceService;

    public HomeService(AppDbContext context, IAppDevicePresenceService appDevicePresenceService)
    {
        _context = context;
        _appDevicePresenceService = appDevicePresenceService;
    }

    public async Task<HomeDashboardViewModel> GetDashboardAsync()
    {
        var activePois = await _context.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        var narrationHistoryCount = await _context.NarrationHistories.CountAsync();
        var today = DateTime.Today;
        var gpsNarrationCount = await _context.NarrationHistories.CountAsync(x =>
            x.TriggerSource == "gps" ||
            x.TriggerSource == "auto");
        var durationQuery = _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0);
        var averageListenSampleCount = await durationQuery.CountAsync();
        var averageListenSeconds = await _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0)
            .Select(x => (double?)x.DurationSeconds!.Value)
            .AverageAsync() ?? 0;
        var presence = await _appDevicePresenceService.GetSummaryAsync();

        return new HomeDashboardViewModel
        {
            PoiCount = await _context.Pois.CountAsync(),
            ActivePoiCount = activePois.Count,
            ActiveQrCount = await _context.QrCodeItems.CountAsync(x => x.IsActive),
            ActiveDeviceCount = presence.ActiveDeviceCount,
            ActiveUserCount = presence.ActiveUserCount,
            DeviceTimeoutSeconds = presence.TimeoutSeconds,
            ActiveDevices = presence.Devices,
            NarrationHistoryCount = narrationHistoryCount,
            TodayNarrationCount = await _context.NarrationHistories.CountAsync(x => x.PlayedAt >= today),
            ConfiguredLanguageCount = CountConfiguredLanguages(activePois),
            GpsNarrationCount = gpsNarrationCount,
            QrNarrationCount = await _context.NarrationHistories.CountAsync(x => x.TriggerSource == "qr"),
            ManualNarrationCount = await _context.NarrationHistories.CountAsync(x => x.TriggerSource == "manual"),
            ActiveTourCount = await _context.Tours.CountAsync(x => x.IsActive),
            AverageListenSeconds = averageListenSeconds,
            AverageListenSampleCount = averageListenSampleCount,
            RecentNarrations = await _context.NarrationHistories
                .AsNoTracking()
                .OrderByDescending(x => x.PlayedAt)
                .Take(6)
                .Select(x => new RecentNarrationItemViewModel
                {
                    PoiId = x.PoiId,
                    PoiName = x.PoiName,
                    UserKey = x.UserKey,
                    Language = x.Language,
                    TriggerSource = x.TriggerSource,
                    Mode = x.Mode,
                    PlayedAt = x.PlayedAt
                })
                .ToListAsync(),
            LanguageBreakdown = await GetBreakdownAsync(
                x => x.Language,
                value => value.ToUpperInvariant()),
            TriggerSourceBreakdown = await GetBreakdownAsync(
                x => x.TriggerSource,
                MapSourceLabel),
            PlaybackModeBreakdown = await GetBreakdownAsync(
                x => x.Mode,
                value => value.ToUpperInvariant()),
            TopPois = await _context.NarrationHistories
                .AsNoTracking()
                .GroupBy(x => new { x.PoiId, x.PoiName })
                .Select(x => new TopPoiPerformanceViewModel
                {
                    PoiId = x.Key.PoiId,
                    PoiName = x.Key.PoiName,
                    Count = x.Count(),
                    LatestPlayedAt = x.Max(item => item.PlayedAt)
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PoiName)
                .Take(5)
                .ToListAsync()
        };
    }

    private async Task<List<DashboardBreakdownItemViewModel>> GetBreakdownAsync(
        Expression<Func<NarrationHistory, string>> selector,
        Func<string, string> labelMapper)
    {
        var items = await _context.NarrationHistories
            .AsNoTracking()
            .GroupBy(selector)
            .Select(x => new
            {
                Key = x.Key,
                Count = x.Count()
            })
            .ToListAsync();

        return items
            .Select(x => new DashboardBreakdownItemViewModel
            {
                Label = labelMapper(x.Key),
                Count = x.Count
            })
            .GroupBy(x => x.Label)
            .Select(x => new DashboardBreakdownItemViewModel
            {
                Label = x.Key,
                Count = x.Sum(item => item.Count)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .ToList();
    }

    private static int CountConfiguredLanguages(IEnumerable<Poi> pois)
    {
        var count = 0;

        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptVi))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptEn))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptZh))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptJa))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptDe))) count++;

        return count;
    }

    private static string MapSourceLabel(string source)
    {
        return (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "auto" => "GPS",
            "gps" => "GPS",
            "qr" => "QR",
            "manual" => "Thủ công",
            _ => "Khác"
        };
    }
}
