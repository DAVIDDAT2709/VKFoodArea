using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class GeofenceEngine
{
    private readonly HaversineDistanceCalculator _distanceCalculator;
    private readonly CooldownStore _cooldownStore;

    // Cooldown cùng 1 POI để tránh lặp quá nhanh khi đứng yên trong vùng đó
    private static readonly TimeSpan PoiCooldown = TimeSpan.FromSeconds(15);

    // Để bằng 0 để không chặn việc chuyển nhanh giữa 2 POI gần nhau trong lúc demo
    private static readonly TimeSpan GlobalCooldown = TimeSpan.Zero;

    // Cần đứng trong vùng tối thiểu 1 giây mới trigger
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    // Chỉ khi chênh lệch rất nhỏ mới dùng Priority
    private const double PriorityTieThresholdMeters = 1.0;

    // Hướng 2: Web nhập bao nhiêu thì App dùng đúng bấy nhiêu
    private const double TriggerBufferMeters = 0;

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

        if (GlobalCooldown > TimeSpan.Zero &&
    _cooldownStore.IsGlobalCooldownActive(GlobalCooldown, best.Poi.Id))
        {
            return new GeofenceDecision
            {
                PoiId = best.Poi.Id,
                DistanceMeters = best.Distance,
                ShouldTrigger = false,
                Reason = $"Blocked by global cooldown ({best.Poi.Name})"
            };
        }

        if (PoiCooldown > TimeSpan.Zero &&
            _cooldownStore.IsPoiCooldownActive(best.Poi.Id, PoiCooldown))
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
        if (inRange is null || inRange.Count == 0)
            throw new ArgumentException("inRange must not be empty", nameof(inRange));

        if (inRange.Count == 1)
            return inRange[0];

        var nearest = inRange[0];

        var tiedCandidates = inRange
            .Where(x => Math.Abs(x.Distance - nearest.Distance) <= PriorityTieThresholdMeters)
            .OrderByDescending(x => x.Poi.Priority)
            .ThenBy(x => x.Distance)
            .ThenBy(x => x.Poi.Id)
            .ToList();

        return tiedCandidates[0];
    }

    private static double GetTriggerRadiusMeters(Poi poi)
        => poi.RadiusMeters + TriggerBufferMeters;

    private sealed class PoiCandidate
    {
        public required Poi Poi { get; init; }
        public double Distance { get; init; }
    }
}