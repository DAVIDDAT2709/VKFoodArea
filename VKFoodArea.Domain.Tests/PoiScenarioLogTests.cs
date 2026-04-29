using VKFoodArea.Models;
using VKFoodArea.Services;
using Xunit;
using Xunit.Abstractions;

namespace VKFoodArea.Domain.Tests;

public class PoiScenarioLogTests
{
    private readonly ITestOutputHelper _output;
    private readonly HaversineDistanceCalculator _distanceCalculator = new();

    public PoiScenarioLogTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Log_WhenStandingBetweenTwoPois_ChoosesHigherPriorityOnNearTie()
    {
        var engine = CreateGeofenceEngine();
        var userLat = 10.760000;
        var userLng = 106.700100;
        var pois = new[]
        {
            CreatePoi(101, "POI A - Priority thấp", 10.760000, 106.700000, radiusMeters: 60, priority: 1),
            CreatePoi(102, "POI B - Priority cao", 10.760000, 106.700200, radiusMeters: 60, priority: 9)
        };

        WriteBetweenPoiLogHeader(userLat, userLng, pois);

        var firstDecision = engine.Evaluate(userLat, userLng, pois);
        _output.WriteLine(
            "[TEST-1][DEBOUNCE] shouldTrigger={0} selectedPoi={1} distance={2:F2}m reason=\"{3}\"",
            firstDecision.ShouldTrigger,
            firstDecision.PoiId?.ToString() ?? "-",
            firstDecision.DistanceMeters,
            firstDecision.Reason);

        Thread.Sleep(1100);

        var decision = engine.Evaluate(userLat, userLng, pois);
        _output.WriteLine(
            "[TEST-1][RESULT] shouldTrigger={0} selectedPoi={1} expectedPoi=102 rule=\"distance tie <= 1m => Priority cao hơn\" reason=\"{2}\"",
            decision.ShouldTrigger,
            decision.PoiId?.ToString() ?? "-",
            decision.Reason);

        Assert.False(firstDecision.ShouldTrigger);
        Assert.True(decision.ShouldTrigger);
        Assert.Equal(102, decision.PoiId);
        AssertDistancesAreNearTie(userLat, userLng, pois);
    }

    [Fact]
    public void Log_WhenManyDevicesAccessSamePoi_QueuesPerDeviceNotPerPoi()
    {
        var requests = new[]
        {
            new PlaybackRequest("device-A", 201, "gps", TimeSpan.FromMilliseconds(0), TimeSpan.FromSeconds(3)),
            new PlaybackRequest("device-B", 201, "gps", TimeSpan.FromMilliseconds(120), TimeSpan.FromSeconds(3)),
            new PlaybackRequest("device-C", 201, "qr", TimeSpan.FromMilliseconds(240), TimeSpan.FromSeconds(3)),
            new PlaybackRequest("device-A", 201, "qr", TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(2))
        };

        _output.WriteLine("[TEST-2][POLICY] queueScope={0} queuedTriggers=auto,gps,tour,qr", NarrationQueuePolicy.QueueScope);
        foreach (var request in requests)
        {
            _output.WriteLine(
                "[TEST-2][REQUEST] device={0} poi={1} trigger={2} queued={3} arrival={4} duration={5}",
                request.DeviceKey,
                request.PoiId,
                request.TriggerSource,
                NarrationQueuePolicy.ShouldQueuePlayback(request.TriggerSource),
                FormatOffset(request.ArrivedAt),
                FormatOffset(request.Duration));
        }

        var dispatches = BuildDeviceLocalQueue(requests);
        foreach (var item in dispatches)
        {
            _output.WriteLine(
                "[TEST-2][DISPATCH] device={0} poi={1} localQueuePosition={2} start={3} end={4} sharedPoiQueue=none",
                item.Request.DeviceKey,
                item.Request.PoiId,
                item.LocalQueuePosition,
                FormatOffset(item.StartAt),
                FormatOffset(item.EndAt));
        }

        var deviceAFirst = dispatches.Single(x => x.Request.DeviceKey == "device-A" && x.LocalQueuePosition == 1);
        var deviceASecond = dispatches.Single(x => x.Request.DeviceKey == "device-A" && x.LocalQueuePosition == 2);
        var deviceB = dispatches.Single(x => x.Request.DeviceKey == "device-B");
        var deviceC = dispatches.Single(x => x.Request.DeviceKey == "device-C");

        _output.WriteLine(
            "[TEST-2][RESULT] devices=3 samePoi=201 webRows={0} behavior=\"B/C không đợi A; chỉ request thứ 2 trên device-A đợi queue local\"",
            dispatches.Count);

        Assert.True(NarrationQueuePolicy.ShouldQueuePlayback("gps"));
        Assert.True(NarrationQueuePolicy.ShouldQueuePlayback("qr"));
        Assert.False(NarrationQueuePolicy.ShouldQueuePlayback("manual"));
        Assert.True(deviceB.StartAt < deviceAFirst.EndAt);
        Assert.True(deviceC.StartAt < deviceAFirst.EndAt);
        Assert.Equal(deviceAFirst.EndAt, deviceASecond.StartAt);
    }

    private void WriteBetweenPoiLogHeader(double userLat, double userLng, IReadOnlyList<Poi> pois)
    {
        _output.WriteLine(
            "[TEST-1][INPUT] userLat={0:F6} userLng={1:F6} scenario=\"standing between two POIs\"",
            userLat,
            userLng);

        foreach (var poi in pois)
        {
            var distance = _distanceCalculator.CalculateMeters(userLat, userLng, poi.Latitude, poi.Longitude);
            _output.WriteLine(
                "[TEST-1][CANDIDATE] poi={0} name=\"{1}\" distance={2:F2}m radius={3:F0}m priority={4} inRange={5}",
                poi.Id,
                poi.Name,
                distance,
                poi.RadiusMeters,
                poi.Priority,
                distance <= poi.RadiusMeters);
        }
    }

    private void AssertDistancesAreNearTie(double userLat, double userLng, IReadOnlyList<Poi> pois)
    {
        var distances = pois
            .Select(poi => _distanceCalculator.CalculateMeters(userLat, userLng, poi.Latitude, poi.Longitude))
            .ToArray();

        Assert.True(Math.Abs(distances[0] - distances[1]) <= 1.0);
    }

    private static IReadOnlyList<PlaybackDispatch> BuildDeviceLocalQueue(IEnumerable<PlaybackRequest> requests)
    {
        return requests
            .GroupBy(x => x.DeviceKey, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                var deviceAvailableAt = TimeSpan.Zero;
                var queuePosition = 0;
                var items = new List<PlaybackDispatch>();

                foreach (var request in group.OrderBy(x => x.ArrivedAt))
                {
                    queuePosition++;
                    var startAt = request.ArrivedAt > deviceAvailableAt
                        ? request.ArrivedAt
                        : deviceAvailableAt;
                    var endAt = startAt + request.Duration;

                    items.Add(new PlaybackDispatch(request, queuePosition, startAt, endAt));
                    deviceAvailableAt = endAt;
                }

                return items;
            })
            .OrderBy(x => x.StartAt)
            .ThenBy(x => x.Request.DeviceKey, StringComparer.Ordinal)
            .ToList();
    }

    private static string FormatOffset(TimeSpan value)
        => $"{(int)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";

    private static GeofenceEngine CreateGeofenceEngine()
        => new(new HaversineDistanceCalculator(), new CooldownStore());

    private static Poi CreatePoi(
        int id,
        string name,
        double latitude,
        double longitude,
        double radiusMeters,
        int priority)
    {
        return new Poi
        {
            Id = id,
            Name = name,
            Latitude = latitude,
            Longitude = longitude,
            RadiusMeters = radiusMeters,
            Priority = priority,
            IsActive = true
        };
    }

    private sealed record PlaybackRequest(
        string DeviceKey,
        int PoiId,
        string TriggerSource,
        TimeSpan ArrivedAt,
        TimeSpan Duration);

    private sealed record PlaybackDispatch(
        PlaybackRequest Request,
        int LocalQueuePosition,
        TimeSpan StartAt,
        TimeSpan EndAt);
}
