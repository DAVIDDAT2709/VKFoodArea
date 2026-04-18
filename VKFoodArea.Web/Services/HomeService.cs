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
    private readonly ICurrentAdminService _currentAdminService;

    public HomeService(
        AppDbContext context,
        IAppDevicePresenceService appDevicePresenceService,
        ICurrentAdminService currentAdminService)
    {
        _context = context;
        _appDevicePresenceService = appDevicePresenceService;
        _currentAdminService = currentAdminService;
    }

    public async Task<HomeDashboardViewModel> GetDashboardAsync()
    {
        var poisQuery = BuildPoiScope();
        var narrationQuery = BuildNarrationScope();
        var ownerPoiIds = _currentAdminService.IsRestaurantOwner
            ? await poisQuery.Select(x => x.Id).ToListAsync()
            : null;

        var activePois = await poisQuery
            .Where(x => x.IsActive && x.ApprovalStatus == PoiApprovalStatus.Approved)
            .ToListAsync();

        var narrationHistoryCount = await narrationQuery.CountAsync();
        var today = WebDisplayTime.TodayStartUtc;
        var tomorrow = WebDisplayTime.TomorrowStartUtc;
        var gpsNarrationCount = await narrationQuery.CountAsync(x =>
            x.TriggerSource == "gps" ||
            x.TriggerSource == "auto");
        var durationQuery = narrationQuery
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0);
        var averageListenSampleCount = await durationQuery.CountAsync();
        var averageListenSeconds = await durationQuery
            .Select(x => (double?)x.DurationSeconds!.Value)
            .AverageAsync() ?? 0;
        var presence = await _appDevicePresenceService.GetSummaryAsync();

        return new HomeDashboardViewModel
        {
            IsRestaurantOwnerDashboard = _currentAdminService.IsRestaurantOwner,
            PoiCount = await poisQuery.CountAsync(),
            ActivePoiCount = activePois.Count,
            PendingPoiCount = await poisQuery.CountAsync(x => x.ApprovalStatus == PoiApprovalStatus.Pending),
            RejectedPoiCount = await poisQuery.CountAsync(x => x.ApprovalStatus == PoiApprovalStatus.Rejected),
            ActiveQrCount = await CountActiveQrAsync(ownerPoiIds),
            ActiveDeviceCount = presence.ActiveDeviceCount,
            ActiveUserCount = presence.ActiveUserCount,
            DeviceTimeoutSeconds = presence.TimeoutSeconds,
            ActiveDevices = presence.Devices,
            NarrationHistoryCount = narrationHistoryCount,
            TodayNarrationCount = await narrationQuery.CountAsync(x => x.PlayedAt >= today && x.PlayedAt < tomorrow),
            ConfiguredLanguageCount = CountConfiguredLanguages(activePois),
            GpsNarrationCount = gpsNarrationCount,
            QrNarrationCount = await narrationQuery.CountAsync(x => x.TriggerSource == "qr"),
            ManualNarrationCount = await narrationQuery.CountAsync(x => x.TriggerSource == "manual"),
            TourNarrationCount = await narrationQuery.CountAsync(x => x.TriggerSource == "tour"),
            ActiveTourCount = await CountActiveToursAsync(ownerPoiIds),
            AverageListenSeconds = averageListenSeconds,
            AverageListenSampleCount = averageListenSampleCount,
            RecentNarrations = await narrationQuery
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
                narrationQuery,
                x => x.Language,
                value => value.ToUpperInvariant()),
            TriggerSourceBreakdown = await GetBreakdownAsync(
                narrationQuery,
                x => x.TriggerSource,
                MapSourceLabel),
            PlaybackModeBreakdown = await GetBreakdownAsync(
                narrationQuery,
                x => x.Mode,
                value => value.ToUpperInvariant()),
            TopPois = await narrationQuery
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
        IQueryable<NarrationHistory> query,
        Expression<Func<NarrationHistory, string>> selector,
        Func<string, string> labelMapper)
    {
        var items = await query
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

    private IQueryable<Poi> BuildPoiScope()
    {
        var query = _context.Pois.AsNoTracking();

        if (_currentAdminService.IsRestaurantOwner)
        {
            query = _currentAdminService.UserId.HasValue
                ? query.Where(x => x.OwnerAdminUserId == _currentAdminService.UserId.Value)
                : query.Where(x => false);
        }

        return query;
    }

    private IQueryable<NarrationHistory> BuildNarrationScope()
    {
        var query = _context.NarrationHistories
            .Include(x => x.Poi)
            .AsNoTracking();

        if (_currentAdminService.IsRestaurantOwner)
        {
            query = _currentAdminService.UserId.HasValue
                ? query.Where(x => x.Poi != null && x.Poi.OwnerAdminUserId == _currentAdminService.UserId.Value)
                : query.Where(x => false);
        }

        return query;
    }

    private async Task<int> CountActiveQrAsync(IReadOnlyCollection<int>? ownerPoiIds)
    {
        var query = _context.QrCodeItems.AsNoTracking().Where(x => x.IsActive);
        if (ownerPoiIds is null)
            return await query.CountAsync();

        if (ownerPoiIds.Count == 0)
            return 0;

        return await query.CountAsync(x =>
            x.TargetType.ToLower() == QrTargetTypes.Poi &&
            ownerPoiIds.Contains(x.TargetId));
    }

    private async Task<int> CountActiveToursAsync(IReadOnlyCollection<int>? ownerPoiIds)
    {
        var query = _context.Tours.AsNoTracking().Where(x => x.IsActive);
        if (ownerPoiIds is null)
            return await query.CountAsync();

        if (ownerPoiIds.Count == 0)
            return 0;

        return await query.CountAsync(x => x.Stops.Any(stop => ownerPoiIds.Contains(stop.PoiId)));
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
            "tour" => "Tour",
            "manual" => "Thủ công",
            _ => "Khác"
        };
    }
}
