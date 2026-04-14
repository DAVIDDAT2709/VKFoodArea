using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class AnalyticsService : IAnalyticsService
{
    private static readonly TimeSpan RouteSessionGap = TimeSpan.FromMinutes(20);
    private const double RouteJumpSplitMeters = 3000;

    private readonly AppDbContext _context;

    public AnalyticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminMapAnalyticsViewModel> GetAdminMapAnalyticsAsync(
        int movementLogSampleSize = 2000,
        int maxDisplayedRoutes = 12,
        int maxHeatPoints = 300)
    {
        movementLogSampleSize = Math.Clamp(movementLogSampleSize, 200, 5000);
        maxDisplayedRoutes = Math.Clamp(maxDisplayedRoutes, 1, 24);
        maxHeatPoints = Math.Clamp(maxHeatPoints, 50, 500);

        var totalMovementLogs = await _context.UserMovementLogs.CountAsync();
        var recentMovementLogs = await _context.UserMovementLogs
            .AsNoTracking()
            .OrderByDescending(x => x.RecordedAt)
            .Take(movementLogSampleSize)
            .ToListAsync();

        var durationQuery = _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.DurationSeconds.HasValue && x.DurationSeconds.Value > 0);

        var averageListenSampleCount = await durationQuery.CountAsync();
        var averageListenSeconds = await durationQuery
            .Select(x => (double?)x.DurationSeconds!.Value)
            .AverageAsync() ?? 0;

        var topPois = await _context.NarrationHistories
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
            .ToListAsync();

        var distinctAnonymousVisitors = recentMovementLogs
            .Select(x => NormalizeUserKey(x.UserKey))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .Count();

        var routes = BuildRoutes(recentMovementLogs)
            .OrderByDescending(x => x.EndedAt)
            .ThenByDescending(x => x.PointCount)
            .ToList();

        var heatmapPoints = BuildHeatmapPoints(recentMovementLogs, maxHeatPoints);
        var latestMovementAt = recentMovementLogs.FirstOrDefault()?.RecordedAt;

        return new AdminMapAnalyticsViewModel
        {
            TotalMovementLogs = totalMovementLogs,
            AnalyzedMovementLogCount = recentMovementLogs.Count,
            AnonymousVisitorCount = distinctAnonymousVisitors,
            AnonymousRouteCount = routes.Count,
            UnassignedMovementLogCount = recentMovementLogs.Count(x => string.IsNullOrWhiteSpace(NormalizeUserKey(x.UserKey))),
            LatestMovementAt = latestMovementAt,
            AverageListenSeconds = averageListenSeconds,
            AverageListenSampleCount = averageListenSampleCount,
            TopPois = topPois,
            Routes = routes.Take(maxDisplayedRoutes).ToList(),
            HeatmapPoints = heatmapPoints,
            AnalyticsWindowLabel = BuildAnalyticsWindowLabel(recentMovementLogs.Count, movementLogSampleSize)
        };
    }

    private static List<AdminMapRouteViewModel> BuildRoutes(IEnumerable<UserMovementLog> logs)
    {
        var routes = new List<AdminMapRouteViewModel>();
        var routeIndex = 1;

        foreach (var userGroup in logs
                     .Where(x => !string.IsNullOrWhiteSpace(NormalizeUserKey(x.UserKey)))
                     .GroupBy(x => NormalizeUserKey(x.UserKey), StringComparer.Ordinal))
        {
            var orderedLogs = userGroup
                .OrderBy(x => x.RecordedAt)
                .ToList();

            var currentSession = new List<UserMovementLog>();

            foreach (var log in orderedLogs)
            {
                if (currentSession.Count > 0 && ShouldStartNewRoute(currentSession[^1], log))
                {
                    AddRouteIfQualified(routes, currentSession, userGroup.Key, routeIndex++);
                    currentSession = [];
                }

                currentSession.Add(log);
            }

            AddRouteIfQualified(routes, currentSession, userGroup.Key, routeIndex++);
        }

        return routes;
    }

    private static void AddRouteIfQualified(
        ICollection<AdminMapRouteViewModel> routes,
        IReadOnlyList<UserMovementLog> sessionLogs,
        string userKey,
        int routeIndex)
    {
        if (sessionLogs.Count < 2)
            return;

        var totalDistanceMeters = 0d;
        for (var i = 1; i < sessionLogs.Count; i++)
        {
            totalDistanceMeters += CalculateDistanceMeters(
                sessionLogs[i - 1].Latitude,
                sessionLogs[i - 1].Longitude,
                sessionLogs[i].Latitude,
                sessionLogs[i].Longitude);
        }

        var firstPoint = sessionLogs[0];
        var lastPoint = sessionLogs[^1];
        var sourceSummary = string.Join(
            " + ",
            sessionLogs
                .Select(x => NormalizeSource(x.Source))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal));

        routes.Add(new AdminMapRouteViewModel
        {
            RouteId = $"route-{routeIndex}",
            RouteLabel = BuildRouteLabel(userKey),
            SourceSummary = sourceSummary,
            PointCount = sessionLogs.Count,
            StartedAt = firstPoint.RecordedAt,
            EndedAt = lastPoint.RecordedAt,
            DurationMinutes = Math.Max((lastPoint.RecordedAt - firstPoint.RecordedAt).TotalMinutes, 0),
            ApproxDistanceMeters = totalDistanceMeters,
            Points = sessionLogs
                .Select(x => new AdminMapGeoPointViewModel
                {
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    RecordedAt = x.RecordedAt,
                    AccuracyMeters = x.AccuracyMeters
                })
                .ToList()
        });
    }

    private static bool ShouldStartNewRoute(UserMovementLog previous, UserMovementLog current)
    {
        if (current.RecordedAt - previous.RecordedAt > RouteSessionGap)
            return true;

        return CalculateDistanceMeters(
                   previous.Latitude,
                   previous.Longitude,
                   current.Latitude,
                   current.Longitude) > RouteJumpSplitMeters;
    }

    private static List<AdminMapHeatPointViewModel> BuildHeatmapPoints(
        IEnumerable<UserMovementLog> logs,
        int maxHeatPoints)
    {
        var buckets = logs
            .GroupBy(x => new
            {
                Latitude = Math.Round(x.Latitude, 4),
                Longitude = Math.Round(x.Longitude, 4)
            })
            .Select(x => new
            {
                Latitude = x.Average(item => item.Latitude),
                Longitude = x.Average(item => item.Longitude),
                Count = x.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(maxHeatPoints)
            .ToList();

        var maxCount = buckets.Count == 0 ? 1 : buckets.Max(x => x.Count);

        return buckets
            .Select(x => new AdminMapHeatPointViewModel
            {
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                Count = x.Count,
                Weight = maxCount <= 0
                    ? 0
                    : (double)x.Count / maxCount
            })
            .ToList();
    }

    private static string BuildAnalyticsWindowLabel(int analyzedCount, int sampleSize)
    {
        if (analyzedCount == 0)
            return "Chưa có dữ liệu di chuyển để hiển thị.";

        return analyzedCount >= sampleSize
            ? $"Đang phân tích {sampleSize:N0} log di chuyển gần nhất."
            : $"Đang phân tích {analyzedCount:N0} log di chuyển đã đồng bộ.";
    }

    private static string BuildRouteLabel(string userKey)
    {
        var normalized = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalized))
            return "Ẩn danh";

        var suffixLength = Math.Min(6, normalized.Length);
        var suffix = normalized[^suffixLength..].ToUpperInvariant();
        return $"Ẩn danh {suffix}";
    }

    private static string NormalizeUserKey(string? userKey)
        => MovementLogUserKeyPrivacy.NormalizeForStorage(userKey);

    private static string NormalizeSource(string? source)
    {
        return (source ?? "gps").Trim().ToLowerInvariant() switch
        {
            "background" => "GPS nền",
            "foreground" => "GPS trực tiếp",
            _ => "GPS"
        };
    }

    private static double CalculateDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusMeters = 6371000;
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var startLatitude = DegreesToRadians(latitude1);
        var endLatitude = DegreesToRadians(latitude2);

        var a =
            Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2) +
            Math.Cos(startLatitude) * Math.Cos(endLatitude) *
            Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value)
        => value * Math.PI / 180.0;
}
