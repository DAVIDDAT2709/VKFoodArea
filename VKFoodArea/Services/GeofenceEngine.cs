using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class GeofenceEngine
{
    private readonly HaversineDistanceCalculator _distanceCalculator;
    private readonly CooldownStore _cooldownStore;

    private static readonly TimeSpan PoiCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GlobalCooldown = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Nếu khoảng cách giữa 2 quán chỉ lệch rất ít thì mới dùng Priority để phân xử.
    /// </summary>
    private const double PriorityTieThresholdMeters = 5;
    private const double TriggerBufferMeters = 12;

    public GeofenceEngine(
        HaversineDistanceCalculator distanceCalculator,
        CooldownStore cooldownStore)
    {
        _distanceCalculator = distanceCalculator;
        _cooldownStore = cooldownStore;
    }

    public GeofenceDecision Evaluate(double userLat, double userLng, IEnumerable<Poi> pois)
    {
        var candidates = pois
            .Where(p => p.IsActive)
            .Select(p => new PoiCandidate
            {
                Poi = p,
                Distance = _distanceCalculator.CalculateMeters(userLat, userLng, p.Latitude, p.Longitude)
            })
            .ToList();

        foreach (var item in candidates)
        {
            if (item.Distance <= GetTriggerRadiusMeters(item.Poi))
                _cooldownStore.MarkInside(item.Poi.Id);
            else
                _cooldownStore.MarkOutside(item.Poi.Id);
        }

        var inRange = candidates
            .Where(x => x.Distance <= GetTriggerRadiusMeters(x.Poi))
            .OrderBy(x => x.Distance)
            .ToList();

        if (inRange.Count == 0)
        {
            return new GeofenceDecision
            {
                ShouldTrigger = false,
                Reason = "No POI in range"
            };
        }

        var best = PickBestCandidate(inRange);

        if (!_cooldownStore.PassedDebounce(best.Poi.Id, Debounce))
        {
            return new GeofenceDecision
            {
                PoiId = best.Poi.Id,
                DistanceMeters = best.Distance,
                ShouldTrigger = false,
                Reason = $"Blocked by debounce ({best.Poi.Name})"
            };
        }

        if (_cooldownStore.IsGlobalCooldownActive(GlobalCooldown))
        {
            return new GeofenceDecision
            {
                PoiId = best.Poi.Id,
                DistanceMeters = best.Distance,
                ShouldTrigger = false,
                Reason = $"Blocked by global cooldown ({best.Poi.Name})"
            };
        }

        if (_cooldownStore.IsPoiCooldownActive(best.Poi.Id, PoiCooldown))
        {
            return new GeofenceDecision
            {
                PoiId = best.Poi.Id,
                DistanceMeters = best.Distance,
                ShouldTrigger = false,
                Reason = $"Blocked by POI cooldown ({best.Poi.Name})"
            };
        }

        _cooldownStore.MarkPlayed(best.Poi.Id);

        return new GeofenceDecision
        {
            PoiId = best.Poi.Id,
            DistanceMeters = best.Distance,
            ShouldTrigger = true,
            Reason = $"Trigger narration ({best.Poi.Name}, {best.Distance:F1}m)"
        };
    }

    private static PoiCandidate PickBestCandidate(List<PoiCandidate> inRange)
    {
        var nearest = inRange[0];

        var tiedCandidates = inRange
            .Where(x => Math.Abs(x.Distance - nearest.Distance) <= PriorityTieThresholdMeters)
            .OrderByDescending(x => x.Poi.Priority)
            .ThenBy(x => x.Distance)
            .ToList();

        return tiedCandidates[0];
    }

    private static double GetTriggerRadiusMeters(Poi poi) => poi.RadiusMeters + TriggerBufferMeters;

    private sealed class PoiCandidate
    {
        public required Poi Poi { get; init; }
        public double Distance { get; init; }
    }
}
