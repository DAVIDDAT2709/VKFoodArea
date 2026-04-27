using System.Net;
using VKFoodArea.Helpers;
using VKFoodArea.Models;
using VKFoodArea.Services;
using Xunit;

namespace VKFoodArea.Domain.Tests;

public class CoreLogicTests
{
    [Fact]
    public void Geofence_SelectsNearestPoiAfterDebounce()
    {
        var engine = CreateGeofenceEngine();
        var pois = new[]
        {
            CreatePoi(1, "Near", 10.760000, 106.700000, radiusMeters: 80, priority: 1),
            CreatePoi(2, "Far", 10.760450, 106.700450, radiusMeters: 80, priority: 9)
        };

        engine.Evaluate(10.760010, 106.700010, pois);
        Thread.Sleep(1100);

        var decision = engine.Evaluate(10.760010, 106.700010, pois);

        Assert.True(decision.ShouldTrigger);
        Assert.Equal(1, decision.PoiId);
    }

    [Fact]
    public void Geofence_BreaksNearTieByHigherPriority()
    {
        var engine = CreateGeofenceEngine();
        var pois = new[]
        {
            CreatePoi(1, "Priority 1", 10.760000, 106.700000, radiusMeters: 80, priority: 1),
            CreatePoi(2, "Priority 9", 10.760000, 106.700000, radiusMeters: 80, priority: 9)
        };

        engine.Evaluate(10.760000, 106.700000, pois);
        Thread.Sleep(1100);

        var decision = engine.Evaluate(10.760000, 106.700000, pois);

        Assert.True(decision.ShouldTrigger);
        Assert.Equal(2, decision.PoiId);
    }

    [Fact]
    public void Geofence_BlocksImmediateReplayByPoiCooldown()
    {
        var engine = CreateGeofenceEngine();
        var pois = new[]
        {
            CreatePoi(1, "Only poi", 10.760000, 106.700000, radiusMeters: 80, priority: 1)
        };

        engine.Evaluate(10.760000, 106.700000, pois);
        Thread.Sleep(1100);

        var firstDecision = engine.Evaluate(10.760000, 106.700000, pois);
        var secondDecision = engine.Evaluate(10.760000, 106.700000, pois);

        Assert.True(firstDecision.ShouldTrigger);
        Assert.False(secondDecision.ShouldTrigger);
        Assert.Contains("cooldown", secondDecision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AppUserRoleNames.Admin, true, true)]
    [InlineData(AppUserRoleNames.Operator, true, true)]
    [InlineData(AppUserRoleNames.User, true, false)]
    [InlineData(AppUserRoleNames.Guest, true, false)]
    [InlineData(AppUserRoleNames.Admin, false, false)]
    public void InternalToolsPolicy_IsRoleAndBuildAware(string role, bool internalToolsEnabled, bool expected)
    {
        var actual = AppFeatureAccessPolicy.CanUseInternalTools(role, internalToolsEnabled);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(AppUserRoleNames.Admin, true, true)]
    [InlineData(AppUserRoleNames.Operator, true, false)]
    [InlineData(AppUserRoleNames.User, true, false)]
    [InlineData(AppUserRoleNames.Admin, false, false)]
    public void EndpointOverridePolicy_IsAdminOnlyOnInternalBuild(string role, bool internalToolsEnabled, bool expected)
    {
        var actual = AppFeatureAccessPolicy.CanOverrideRemoteEndpoint(role, internalToolsEnabled);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 1, true)]
    [InlineData(HttpStatusCode.TooManyRequests, 2, true)]
    [InlineData(HttpStatusCode.BadRequest, 1, false)]
    [InlineData(null, 1, true)]
    [InlineData(HttpStatusCode.InternalServerError, 7, false)]
    public void RetryPolicy_OnlyRetriesTransientFailures(HttpStatusCode? statusCode, int nextAttemptCount, bool expected)
    {
        var actual = AppSyncRetryPolicy.ShouldRetry(statusCode, nextAttemptCount);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RetryPolicy_UsesExpectedBackoffSchedule()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), AppSyncRetryPolicy.GetDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(1), AppSyncRetryPolicy.GetDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(5), AppSyncRetryPolicy.GetDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(15), AppSyncRetryPolicy.GetDelay(4));
        Assert.Equal(TimeSpan.FromMinutes(30), AppSyncRetryPolicy.GetDelay(5));
        Assert.Equal(TimeSpan.FromHours(1), AppSyncRetryPolicy.GetDelay(6));
        Assert.Equal(TimeSpan.FromHours(1), AppSyncRetryPolicy.GetDelay(10));
    }

    [Fact]
    public void QrPayload_KeepsCustomSchemePayloadIntact()
    {
        var normalized = QrCodePayload.Normalize("poi:oc-vu");
        Assert.Equal("poi:oc-vu", normalized);
    }

    [Fact]
    public void QrPayload_ExtractsCodeFromStructuredLink()
    {
        var normalized = QrCodePayload.Normalize("https://example.com/qr/poi:oc-vu?source=http%3A%2F%2Flocalhost");
        Assert.Equal("poi:oc-vu", normalized);
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
}
