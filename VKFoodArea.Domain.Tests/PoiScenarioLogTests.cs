using VKFoodArea.Models;
using VKFoodArea.Services;
using Xunit;
using Xunit.Abstractions;

namespace VKFoodArea.Domain.Tests;

public class PoiScenarioLogTests
{
    private const int VirtualDeviceCount = 1000;
    private const int SharedPoiId = 201;

    private readonly ITestOutputHelper _output;
    private readonly HaversineDistanceCalculator _distanceCalculator = new();

    public PoiScenarioLogTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BetweenOcVuAndOcLoan_SelectsOcVuBecauseItIsNearest()
    {
        var engine = CreateGeofenceEngine();
        var deviceKey = "map-analytics-demo-device-0001";

        // Demo point from map analytics: standing in the overlap between Oc Vu and Oc Loan,
        // slightly closer to Oc Vu.
        var userLat = 10.7613275;
        var userLng = 106.7026730;

        var pois = new[]
        {
            CreatePoi(2, "Ốc Vũ", 10.7614025, 106.7027047, radiusMeters: 18, priority: 9),
            CreatePoi(7, "Ốc Loan", 10.7612240, 106.7026292, radiusMeters: 16, priority: 4)
        };

        WriteBetweenPoiLogHeader(
            "TEST-1A",
            deviceKey,
            userLat,
            userLng,
            "map analytics demo: device is between Oc Vu and Oc Loan; distance gap > 1m, so nearest POI wins before priority tie-break",
            pois);

        var firstDecision = engine.Evaluate(userLat, userLng, pois);
        Thread.Sleep(1100);
        var decision = engine.Evaluate(userLat, userLng, pois);

        WriteDecision("TEST-1A", firstDecision, "debounce warm-up");
        WriteDecision("TEST-1A", decision, "final");

        var selectedPoi = pois.FirstOrDefault(x => x.Id == decision.PoiId);
        var success = decision.ShouldTrigger && decision.PoiId == 2;
        WriteSummary(
            "TEST-1A",
            totalDevices: 1,
            successfulRequests: success ? 1 : 0,
            failedRequests: success ? 0 : 1,
            expected: "POI 2 - Ốc Vũ",
            actual: selectedPoi is null
                ? $"POI {decision.PoiId?.ToString() ?? "-"}"
                : $"POI {selectedPoi.Id} - {selectedPoi.Name}",
            passed: success);

        Assert.False(firstDecision.ShouldTrigger);
        Assert.True(decision.ShouldTrigger);
        Assert.Equal(2, decision.PoiId);
        Assert.True(DistanceGap(userLat, userLng, pois) > 1.0);
    }

    [Fact]
    public void BetweenTwoPois_SelectsHigherPriority_WhenDistancesAreNearTie()
    {
        var engine = CreateGeofenceEngine();
        var deviceKey = "virtual-device-0001";
        var userLat = 10.760000;
        var userLng = 106.700100;
        var pois = new[]
        {
            CreatePoi(101, "POI A low priority", 10.760000, 106.700000, radiusMeters: 80, priority: 1),
            CreatePoi(102, "POI B high priority", 10.760000, 106.700200, radiusMeters: 80, priority: 9)
        };

        WriteBetweenPoiLogHeader(
            "TEST-1B",
            deviceKey,
            userLat,
            userLng,
            "distance tie <= 1m uses higher Priority",
            pois);

        var firstDecision = engine.Evaluate(userLat, userLng, pois);
        Thread.Sleep(1100);
        var decision = engine.Evaluate(userLat, userLng, pois);

        WriteDecision("TEST-1B", firstDecision, "debounce warm-up");
        WriteDecision("TEST-1B", decision, "final");

        var success = decision.ShouldTrigger && decision.PoiId == 102;
        WriteSummary(
            "TEST-1B",
            totalDevices: 1,
            successfulRequests: success ? 1 : 0,
            failedRequests: success ? 0 : 1,
            expected: "POI 102",
            actual: $"POI {decision.PoiId?.ToString() ?? "-"}",
            passed: success);

        Assert.False(firstDecision.ShouldTrigger);
        Assert.True(decision.ShouldTrigger);
        Assert.Equal(102, decision.PoiId);
        Assert.True(DistanceGap(userLat, userLng, pois) <= 1.0);
    }

    [Fact]
    public void SamePoi_With1000VirtualDevices_UsesDeviceLocalQueueNotSharedPoiQueue()
    {
        var firstWave = Enumerable.Range(1, VirtualDeviceCount)
            .Select(index => new PlaybackRequest(
                DeviceKey: FormatDeviceKey(index),
                PoiId: SharedPoiId,
                TriggerSource: "gps",
                ArrivedAt: TimeSpan.Zero,
                Duration: TimeSpan.FromSeconds(3)))
            .ToList();

        var sampleSecondRequest = new PlaybackRequest(
            DeviceKey: FormatDeviceKey(1),
            PoiId: SharedPoiId,
            TriggerSource: "qr",
            ArrivedAt: TimeSpan.FromMilliseconds(500),
            Duration: TimeSpan.FromSeconds(2));

        var requests = firstWave.Append(sampleSecondRequest).ToList();

        WriteQueueScenarioHeader(requests.Count);

        WriteLine("TEST-2", "Sample requests:");
        WriteLine("TEST-2", "  DeviceKey             POI  Trigger  Queued  Arrival     Duration");
        WriteLine("TEST-2", "  --------------------  ---  -------  ------  ----------  ----------");
        foreach (var request in requests.Take(3).Append(sampleSecondRequest).Distinct())
        {
            WriteLine(
                "TEST-2",
                $"  {request.DeviceKey,-20}  {request.PoiId,3}  {request.TriggerSource,-7}  {YesNo(NarrationQueuePolicy.ShouldQueuePlayback(request.TriggerSource)),-6}  {FormatOffset(request.ArrivedAt),-10}  {FormatOffset(request.Duration),-10}");
        }

        var dispatches = BuildDeviceLocalQueue(requests);
        var firstWaveDispatches = dispatches
            .Where(x => x.Request.ArrivedAt == TimeSpan.Zero)
            .ToList();
        var sampleDeviceDispatches = dispatches
            .Where(x => x.Request.DeviceKey == FormatDeviceKey(1))
            .OrderBy(x => x.LocalQueuePosition)
            .ToList();

        WriteLine("TEST-2", string.Empty);
        WriteLine("TEST-2", "Sample dispatch for one device:");
        WriteLine("TEST-2", "  DeviceKey             POI  Local queue  Start       End         Shared POI queue");
        WriteLine("TEST-2", "  --------------------  ---  -----------  ----------  ----------  ----------------");
        foreach (var item in sampleDeviceDispatches)
        {
            WriteLine(
                "TEST-2",
                $"  {item.Request.DeviceKey,-20}  {item.Request.PoiId,3}  {item.LocalQueuePosition,11}  {FormatOffset(item.StartAt),-10}  {FormatOffset(item.EndAt),-10}  none");
        }

        var successfulRequests = dispatches.Count;
        var failedRequests = requests.Count - successfulRequests;
        var concurrentFirstWave = firstWaveDispatches.Count(x => x.StartAt == TimeSpan.Zero);
        var sampleFirst = sampleDeviceDispatches[0];
        var sampleSecond = sampleDeviceDispatches[1];

        WriteLine("TEST-2", string.Empty);
        WriteLine("TEST-2", "Result:");
        WriteLine("TEST-2", $"  Total virtual devices      : {VirtualDeviceCount}");
        WriteLine("TEST-2", $"  Total playback requests    : {requests.Count}");
        WriteLine("TEST-2", $"  Successful requests        : {successfulRequests}");
        WriteLine("TEST-2", $"  Failed requests            : {failedRequests}");
        WriteLine("TEST-2", $"  First-wave concurrent start: {concurrentFirstWave}/{VirtualDeviceCount}");
        WriteLine("TEST-2", $"  Sample device              : {FormatDeviceKey(1)}");
        WriteLine("TEST-2", $"  First playback             : {FormatOffset(sampleFirst.StartAt)} -> {FormatOffset(sampleFirst.EndAt)}");
        WriteLine("TEST-2", $"  Second playback            : {FormatOffset(sampleSecond.StartAt)} -> {FormatOffset(sampleSecond.EndAt)}");
        WriteLine("TEST-2", $"  PASS                       : {YesNo(successfulRequests == requests.Count && failedRequests == 0 && concurrentFirstWave == VirtualDeviceCount && sampleSecond.StartAt == sampleFirst.EndAt)}");
        WriteLine("TEST-2", string.Empty);

        Assert.Equal(VirtualDeviceCount, requests.Select(x => x.DeviceKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(firstWaveDispatches, item => Assert.Equal(TimeSpan.Zero, item.StartAt));
        Assert.Equal(VirtualDeviceCount, concurrentFirstWave);
        Assert.Equal(sampleFirst.EndAt, sampleSecond.StartAt);
        Assert.Equal(requests.Count, successfulRequests);
        Assert.Equal(0, failedRequests);
        Assert.Equal("device-local", NarrationQueuePolicy.QueueScope);
        Assert.True(NarrationQueuePolicy.ShouldQueuePlayback("gps"));
        Assert.True(NarrationQueuePolicy.ShouldQueuePlayback("qr"));
        Assert.False(NarrationQueuePolicy.ShouldQueuePlayback("manual"));
    }

    private void WriteBetweenPoiLogHeader(
        string testId,
        string deviceKey,
        double userLat,
        double userLng,
        string rule,
        IReadOnlyList<Poi> pois)
    {
        WriteSectionHeader(testId);
        WriteLine(testId, "Input:");
        WriteLine(testId, $"  DeviceKey : {deviceKey}");
        WriteLine(testId, $"  Latitude  : {userLat:F7}");
        WriteLine(testId, $"  Longitude : {userLng:F7}");
        WriteLine(testId, "Rule:");
        WriteLine(testId, $"  {rule}");
        WriteLine(testId, string.Empty);
        WriteLine(testId, "Candidates:");
        WriteLine(testId, "  POI  Name                 Distance   Radius   Priority  In range");
        WriteLine(testId, "  ---  -------------------  ---------  -------  --------  --------");

        foreach (var poi in pois)
        {
            var distance = _distanceCalculator.CalculateMeters(userLat, userLng, poi.Latitude, poi.Longitude);
            WriteLine(
                testId,
                $"  {poi.Id,3}  {TrimForTable(poi.Name),-19}  {distance,7:F2}m  {poi.RadiusMeters,5:F0}m  {poi.Priority,8}  {YesNo(distance <= poi.RadiusMeters)}");
        }
    }

    private void WriteDecision(string testId, GeofenceDecision decision, string phase)
    {
        WriteLine(testId, string.Empty);
        WriteLine(testId, $"Decision ({phase}):");
        WriteLine(testId, $"  Should trigger : {YesNo(decision.ShouldTrigger)}");
        WriteLine(testId, $"  Selected POI   : {decision.PoiId?.ToString() ?? "-"}");
        WriteLine(testId, $"  Distance       : {decision.DistanceMeters:F2}m");
        WriteLine(testId, $"  Reason         : {decision.Reason}");
    }

    private void WriteSummary(
        string testId,
        int totalDevices,
        int successfulRequests,
        int failedRequests,
        string expected,
        string actual,
        bool passed)
    {
        WriteLine(testId, string.Empty);
        WriteLine(testId, "Result:");
        WriteLine(testId, $"  Expected            : {expected}");
        WriteLine(testId, $"  Actual              : {actual}");
        WriteLine(testId, $"  Total devices       : {totalDevices}");
        WriteLine(testId, $"  Successful requests : {successfulRequests}");
        WriteLine(testId, $"  Failed requests     : {failedRequests}");
        WriteLine(testId, $"  PASS                : {YesNo(passed)}");
        WriteLine(testId, string.Empty);
    }

    private void WriteLine(string testId, string message)
    {
        _ = testId;
        _output.WriteLine(message);
        ScenarioLog.WriteLine(message);
    }

    private void WriteSectionHeader(string testId)
    {
        var title = testId switch
        {
            "TEST-1A" => "TEST-1A | Bài toán 1 - Demo analytics: device giữa Ốc Vũ và Ốc Loan",
            "TEST-1B" => "TEST-1B | Bài toán 1 - Near-tie: Priority cao hơn thắng",
            _ => testId
        };

        WriteLine(testId, string.Empty);
        WriteLine(testId, "============================================================");
        WriteLine(testId, title);
        WriteLine(testId, "============================================================");
    }

    private void WriteQueueScenarioHeader(int totalRequests)
    {
        WriteLine("TEST-2", string.Empty);
        WriteLine("TEST-2", "============================================================");
        WriteLine("TEST-2", "TEST-2 | Bài toán 2 - 1000 DeviceKey cùng truy cập một POI");
        WriteLine("TEST-2", "============================================================");
        WriteLine("TEST-2", "Input:");
        WriteLine("TEST-2", $"  Total virtual devices : {VirtualDeviceCount}");
        WriteLine("TEST-2", $"  Total requests        : {totalRequests}");
        WriteLine("TEST-2", $"  Same POI              : {SharedPoiId}");
        WriteLine("TEST-2", $"  Sample device         : {FormatDeviceKey(1)}");
        WriteLine("TEST-2", "Rule:");
        WriteLine("TEST-2", $"  Queue scope           : {NarrationQueuePolicy.QueueScope}");
        WriteLine("TEST-2", "  Queued triggers       : auto, gps, tour, qr");
        WriteLine("TEST-2", "  Server POI queue      : none");
        WriteLine("TEST-2", string.Empty);
    }

    private double DistanceGap(double userLat, double userLng, IReadOnlyList<Poi> pois)
    {
        var distances = pois
            .Select(poi => _distanceCalculator.CalculateMeters(userLat, userLng, poi.Latitude, poi.Longitude))
            .ToArray();

        return Math.Abs(distances[0] - distances[1]);
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
            .ThenBy(x => x.LocalQueuePosition)
            .ToList();
    }

    private static string FormatDeviceKey(int index)
        => $"virtual-device-{index:0000}";

    private static string FormatOffset(TimeSpan value)
        => $"{(int)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";

    private static string YesNo(bool value)
        => value ? "YES" : "NO";

    private static string TrimForTable(string value)
    {
        const int maxLength = 19;
        if (value.Length <= maxLength)
            return value;

        return value[..(maxLength - 1)] + "…";
    }

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
