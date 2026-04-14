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
    string LastGeofenceReason,
    TourSession? ActiveTourSession);

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
    private readonly TourSessionService _tourSessionService;
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
    private TourSession? _activeTourSession;
    private DateTimeOffset _lastMovementSyncedAt = DateTimeOffset.MinValue;
    private Location? _lastMovementSyncedLocation;
    private static readonly TimeSpan AutoNarrationCooldown = TimeSpan.FromSeconds(8);
    private readonly object _autoNarrationSync = new();
    private DateTimeOffset _lastAutoNarrationAt = DateTimeOffset.MinValue;
    private int? _lastAutoNarratedPoiId;

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
        AuthService authService,
        TourSessionService tourSessionService)
    {
        _poiService = poiService;
        _locationTrackerService = locationTrackerService;
        _geofenceEngine = geofenceEngine;
        _narrationService = narrationService;
        _permissionService = permissionService;
        _trackingPolicyService = trackingPolicyService;
        _movementLogSyncService = movementLogSyncService;
        _authService = authService;
        _tourSessionService = tourSessionService;
        _currentLocation = DefaultLocation;
        _activeTourSession = _tourSessionService.GetCurrentSession();

        _locationTrackerService.LocationChanged += OnLocationChanged;
        _trackingPolicyService.ProfileChanged += OnTrackingProfileChanged;
        _tourSessionService.StateChanged += OnTourSessionStateChanged;
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
                _lastGeofenceReason,
                _activeTourSession);
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
                geofenceReason: string.Empty,
                activeTourSession: _tourSessionService.GetCurrentSession());
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
                geofenceReason: string.Empty,
                activeTourSession: snapshot.ActiveTourSession);
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
                    geofenceReason: string.Empty,
                    activeTourSession: snapshot.ActiveTourSession);
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
                geofenceReason: string.Empty,
                activeTourSession: snapshot.ActiveTourSession);
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

    private void OnTourSessionStateChanged(object? sender, EventArgs e)
    {
        lock (_snapshotSync)
        {
            _activeTourSession = _tourSessionService.GetCurrentSession();
            _nearestPoi = FindPriorityPoi(_currentLocation, _pois, _activeTourSession);
        }

        RaiseStateChanged();
    }

    private async Task HandleLocationChangedAsync(Location location, CancellationToken ct = default)
    {
        Poi? poiToPlay = null;
        var shouldAdvanceTour = false;
        var trackingProfile = _trackingPolicyService.GetCurrentProfile();
        var hasPendingTourStop = false;

        await _stateLock.WaitAsync(ct);
        try
        {
            var pois = await _poiService.GetAllPoisAsync(ct);
            var activeTourSession = _tourSessionService.GetCurrentSession();
            var decision = _geofenceEngine.Evaluate(location.Latitude, location.Longitude, pois);
            var geofenceReason = decision.Reason;

            if (activeTourSession?.CurrentStop is { PoiId: > 0 } currentStop)
            {
                hasPendingTourStop = true;
                var stopPoi = pois.FirstOrDefault(x => x.Id == currentStop.PoiId) ?? currentStop.Poi;
                if (stopPoi is not null)
                {
                    var distanceToStopMeters = Location.CalculateDistance(
                        location.Latitude,
                        location.Longitude,
                        stopPoi.Latitude,
                        stopPoi.Longitude,
                        DistanceUnits.Kilometers) * 1000;

                    if (distanceToStopMeters <= stopPoi.RadiusMeters + 12)
                    {
                        poiToPlay = stopPoi;
                        shouldAdvanceTour = true;
                        geofenceReason = $"Tour stop reached: {stopPoi.Name}";
                    }
                    else
                    {
                        geofenceReason = $"Tour next stop: {stopPoi.Name} ({distanceToStopMeters:F0}m)";
                    }
                }
            }

            ApplySnapshot(
                location,
                pois,
                isInitialized: true,
                hasLocationPermission: true,
                isGpsListening: true,
                trackingMode: trackingProfile.Mode,
                trackingPolicyName: trackingProfile.PolicyName,
                geofenceReason: geofenceReason,
                activeTourSession: activeTourSession);

            if (!hasPendingTourStop &&
                poiToPlay is null &&
                decision.ShouldTrigger &&
                decision.PoiId.HasValue)
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

        if (poiToPlay is not { } poi)
            return;

        if (_narrationService.IsManualPlaybackActive())
            return;

        if (_narrationService.IsPoiPlaybackActive(poi.Id))
            return;

        if (!ShouldAutoPlayPoi(poi.Id))
            return;

        await _narrationService.PlayPoiAsync(
            poi.Id,
            shouldAdvanceTour ? "tour" : "auto",
            ct: ct);

        if (shouldAdvanceTour)
        {
            _tourSessionService.CompleteCurrentStop();
        }
    }

    private void ApplySnapshot(
        Location location,
        IReadOnlyList<Poi> pois,
        bool isInitialized,
        bool hasLocationPermission,
        bool isGpsListening,
        LocationTrackingMode trackingMode,
        string trackingPolicyName,
        string geofenceReason,
        TourSession? activeTourSession)
    {
        var nearestPoi = FindPriorityPoi(location, pois, activeTourSession);

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
            _activeTourSession = activeTourSession;
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
                geofenceReason: string.Empty,
                activeTourSession: snapshot.ActiveTourSession);
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseStateChanged();
    }

    private static Poi? FindPriorityPoi(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        var currentStopPoi = activeTourSession?.CurrentStop?.Poi;
        if (currentStopPoi?.Id > 0)
        {
            return pois.FirstOrDefault(x => x.Id == currentStopPoi.Id) ?? currentStopPoi;
        }

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

    private bool ShouldAutoPlayPoi(int poiId)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_autoNarrationSync)
        {
            if (_lastAutoNarratedPoiId == poiId &&
                now - _lastAutoNarrationAt < AutoNarrationCooldown)
            {
                return false;
            }

            _lastAutoNarratedPoiId = poiId;
            _lastAutoNarrationAt = now;
            return true;
        }
    }

    private void RaiseStateChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);
}
