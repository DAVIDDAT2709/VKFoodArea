using Microsoft.Maui.Devices.Sensors;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed record PoiRuntimeSnapshot(
    Location CurrentLocation,
    IReadOnlyList<Poi> Pois,
    Poi? NearestPoi,
    bool IsInitialized,
    bool HasLocationPermission,
    bool IsGpsListening,
    LocationTrackingMode TrackingMode,
    string TrackingPolicyName,
    string LastGeofenceReason);

public class PoiRuntimeService
{
    private static readonly TimeSpan MovementSyncInterval = TimeSpan.FromSeconds(30);
    private const double MovementSyncMinDistanceMeters = 20;

    private readonly PoiService _poiService;
    private readonly LocationTrackerService _locationTrackerService;
    private readonly GeofenceEngine _geofenceEngine;
    private readonly NarrationService _narrationService;
    private readonly PermissionService _permissionService;
    private readonly LocationTrackingPolicyService _trackingPolicyService;
    private readonly MovementLogSyncService _movementLogSyncService;
    private readonly AuthService _authService;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly object _snapshotSync = new();
    private readonly object _movementSync = new();

    private List<Poi> _pois = [];
    private Poi? _nearestPoi;
    private bool _isInitialized;
    private bool _hasLocationPermission;
    private bool _isGpsListening;
    private LocationTrackingMode _trackingMode = LocationTrackingMode.ForegroundNavigation;
    private string _trackingPolicyName = "foreground-active";
    private string _lastGeofenceReason = string.Empty;
    private DateTimeOffset _lastMovementSyncedAt = DateTimeOffset.MinValue;
    private Location? _lastMovementSyncedLocation;

    public event EventHandler? StateChanged;

    public Location DefaultLocation { get; } = new(10.7618, 106.7022);

    private Location _currentLocation;

    public PoiRuntimeService(
        PoiService poiService,
        LocationTrackerService locationTrackerService,
        GeofenceEngine geofenceEngine,
        NarrationService narrationService,
        PermissionService permissionService,
        LocationTrackingPolicyService trackingPolicyService,
        MovementLogSyncService movementLogSyncService,
        AuthService authService)
    {
        _poiService = poiService;
        _locationTrackerService = locationTrackerService;
        _geofenceEngine = geofenceEngine;
        _narrationService = narrationService;
        _permissionService = permissionService;
        _trackingPolicyService = trackingPolicyService;
        _movementLogSyncService = movementLogSyncService;
        _authService = authService;
        _currentLocation = DefaultLocation;

        _locationTrackerService.LocationChanged += OnLocationChanged;
        _trackingPolicyService.ProfileChanged += OnTrackingProfileChanged;
    }

    public PoiRuntimeSnapshot GetSnapshot()
    {
        lock (_snapshotSync)
        {
            return new PoiRuntimeSnapshot(
                _currentLocation,
                _pois.ToList(),
                _nearestPoi,
                _isInitialized,
                _hasLocationPermission,
                _isGpsListening,
                _trackingMode,
                _trackingPolicyName,
                _lastGeofenceReason);
        }
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            var pois = await _poiService.GetAllPoisAsync(ct);
            var currentSnapshot = GetSnapshot();
            var location = currentSnapshot.CurrentLocation;
            var trackingProfile = _trackingPolicyService.GetCurrentProfile();
            var hasPermission = await _permissionService.EnsureLocationPermissionAsync(trackingProfile);
            var isGpsListening = currentSnapshot.IsGpsListening;

            if (hasPermission)
            {
                location = await _locationTrackerService.GetCurrentAsync(trackingProfile, ct)
                           ?? await _locationTrackerService.GetLastKnownAsync(ct)
                           ?? location;

                if (!isGpsListening)
                    isGpsListening = await _locationTrackerService.StartListeningAsync(trackingProfile, ct);
            }
            else
            {
                isGpsListening = false;
            }

            ApplySnapshot(
                location,
                pois,
                isInitialized: true,
                hasLocationPermission: hasPermission,
                isGpsListening: isGpsListening,
                trackingMode: trackingProfile.Mode,
                trackingPolicyName: trackingProfile.PolicyName,
                geofenceReason: string.Empty);
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
    }

    public async Task ReloadPoisAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            var snapshot = GetSnapshot();
            var pois = await _poiService.GetAllPoisAsync(ct);

            ApplySnapshot(
                snapshot.CurrentLocation,
                pois,
                isInitialized: snapshot.IsInitialized,
                hasLocationPermission: snapshot.HasLocationPermission,
                isGpsListening: snapshot.IsGpsListening,
                trackingMode: snapshot.TrackingMode,
                trackingPolicyName: snapshot.TrackingPolicyName,
                geofenceReason: string.Empty);
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
    }

    public async Task<bool> RefreshCurrentLocationAsync(CancellationToken ct = default)
    {
        var trackingProfile = _trackingPolicyService.GetCurrentProfile();
        var hasPermission = await _permissionService.EnsureLocationPermissionAsync(trackingProfile);
        if (!hasPermission)
        {
            await _stateLock.WaitAsync(ct);
            try
            {
                var snapshot = GetSnapshot();
                ApplySnapshot(
                    snapshot.CurrentLocation,
                    snapshot.Pois,
                    isInitialized: snapshot.IsInitialized,
                    hasLocationPermission: false,
                    isGpsListening: false,
                    trackingMode: trackingProfile.Mode,
                    trackingPolicyName: trackingProfile.PolicyName,
                    geofenceReason: string.Empty);
            }
            finally
            {
                _stateLock.Release();
            }

            RaiseStateChanged();
            return false;
        }

        var location = await _locationTrackerService.GetCurrentAsync(trackingProfile, ct)
                       ?? await _locationTrackerService.GetLastKnownAsync(ct);

        if (location is null)
            return false;

        await _stateLock.WaitAsync(ct);
        try
        {
            var snapshot = GetSnapshot();
            var isGpsListening = snapshot.IsGpsListening;

            if (!isGpsListening)
                isGpsListening = await _locationTrackerService.StartListeningAsync(trackingProfile, ct);

            ApplySnapshot(
                location,
                snapshot.Pois,
                isInitialized: true,
                hasLocationPermission: true,
                isGpsListening: isGpsListening,
                trackingMode: trackingProfile.Mode,
                trackingPolicyName: trackingProfile.PolicyName,
                geofenceReason: string.Empty);
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
        return true;
    }

    private async void OnLocationChanged(object? sender, Location location)
    {
        try
        {
            await HandleLocationChangedAsync(location);
        }
        catch
        {
        }
    }

    private async void OnTrackingProfileChanged(object? sender, EventArgs e)
    {
        try
        {
            await ApplyTrackingPolicyAsync();
        }
        catch
        {
        }
    }

    private async Task HandleLocationChangedAsync(Location location, CancellationToken ct = default)
    {
        Poi? poiToPlay = null;
        var trackingProfile = _trackingPolicyService.GetCurrentProfile();

        await _stateLock.WaitAsync(ct);
        try
        {
            var pois = await _poiService.GetAllPoisAsync(ct);
            var decision = _geofenceEngine.Evaluate(location.Latitude, location.Longitude, pois);

            ApplySnapshot(
                location,
                pois,
                isInitialized: true,
                hasLocationPermission: true,
                isGpsListening: true,
                trackingMode: trackingProfile.Mode,
                trackingPolicyName: trackingProfile.PolicyName,
                geofenceReason: decision.Reason);

            if (decision.ShouldTrigger && decision.PoiId.HasValue)
            {
                poiToPlay = pois.FirstOrDefault(x => x.Id == decision.PoiId.Value)
                            ?? await _poiService.GetPoiByIdAsync(decision.PoiId.Value, ct);
            }
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
        _ = PushMovementLogIfNeededAsync(location, trackingProfile.Mode, CancellationToken.None);

        if (poiToPlay is not null)
            await _narrationService.PlayPoiAsync(poiToPlay.Id, "auto", ct: ct);
    }

    private void ApplySnapshot(
        Location location,
        IReadOnlyList<Poi> pois,
        bool isInitialized,
        bool hasLocationPermission,
        bool isGpsListening,
        LocationTrackingMode trackingMode,
        string trackingPolicyName,
        string geofenceReason)
    {
        var nearestPoi = FindNearestPoi(location, pois);

        lock (_snapshotSync)
        {
            _currentLocation = location;
            _pois = pois.ToList();
            _nearestPoi = nearestPoi;
            _isInitialized = isInitialized;
            _hasLocationPermission = hasLocationPermission;
            _isGpsListening = isGpsListening;
            _trackingMode = trackingMode;
            _trackingPolicyName = trackingPolicyName;
            _lastGeofenceReason = geofenceReason;
        }
    }

    private async Task ApplyTrackingPolicyAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            var snapshot = GetSnapshot();
            var trackingProfile = _trackingPolicyService.GetCurrentProfile();
            var hasPermission = await _permissionService.EnsureLocationPermissionAsync(
                trackingProfile,
                requestIfNeeded: false);
            var isGpsListening = snapshot.IsGpsListening;
            var location = snapshot.CurrentLocation;

            if (hasPermission)
            {
                isGpsListening = await _locationTrackerService.StartListeningAsync(trackingProfile, ct);

                if (snapshot.IsInitialized)
                {
                    location = await _locationTrackerService.GetCurrentAsync(trackingProfile, ct)
                               ?? await _locationTrackerService.GetLastKnownAsync(ct)
                               ?? location;
                }
            }
            else if (trackingProfile.Mode == LocationTrackingMode.BackgroundMonitoring)
            {
                isGpsListening = false;
            }

            ApplySnapshot(
                location,
                snapshot.Pois,
                isInitialized: snapshot.IsInitialized,
                hasLocationPermission: hasPermission || snapshot.HasLocationPermission,
                isGpsListening: isGpsListening,
                trackingMode: trackingProfile.Mode,
                trackingPolicyName: trackingProfile.PolicyName,
                geofenceReason: string.Empty);
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
    }

    private static Poi? FindNearestPoi(Location location, IEnumerable<Poi> pois)
    {
        return pois
            .Select(poi => new
            {
                Poi = poi,
                Distance = Location.CalculateDistance(
                    location.Latitude,
                    location.Longitude,
                    poi.Latitude,
                    poi.Longitude,
                    DistanceUnits.Kilometers)
            })
            .OrderBy(x => x.Distance)
            .Select(x => x.Poi)
            .FirstOrDefault();
    }

    private async Task PushMovementLogIfNeededAsync(
        Location location,
        LocationTrackingMode trackingMode,
        CancellationToken ct)
    {
        if (!ShouldPushMovementLog(location))
            return;

        try
        {
            await _movementLogSyncService.PushAsync(
                location.Latitude,
                location.Longitude,
                location.Accuracy,
                _authService.GetCurrentUserSyncKey(),
                trackingMode == LocationTrackingMode.BackgroundMonitoring ? "background" : "foreground",
                ct);
        }
        catch
        {
        }
    }

    private bool ShouldPushMovementLog(Location location)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_movementSync)
        {
            var hasWaitedLongEnough = now - _lastMovementSyncedAt >= MovementSyncInterval;
            var hasMovedEnough = _lastMovementSyncedLocation is null ||
                Location.CalculateDistance(
                    _lastMovementSyncedLocation.Latitude,
                    _lastMovementSyncedLocation.Longitude,
                    location.Latitude,
                    location.Longitude,
                    DistanceUnits.Kilometers) * 1000 >= MovementSyncMinDistanceMeters;

            if (!hasWaitedLongEnough && !hasMovedEnough)
                return false;

            _lastMovementSyncedAt = now;
            _lastMovementSyncedLocation = location;
            return true;
        }
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);
}
