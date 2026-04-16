using Microsoft.Maui.Devices.Sensors;

namespace VKFoodArea.Services;

public class LocationTrackerService
{
    public event EventHandler<Location>? LocationChanged;

    private readonly object _demoSync = new();

    private bool _isListening;
    private LocationTrackingProfile? _activeProfile;

    private bool _isDemoModeEnabled;
    private Location? _demoLocation;

    public bool IsDemoModeEnabled
    {
        get
        {
            lock (_demoSync)
                return _isDemoModeEnabled;
        }
    }

    public Location? DemoLocation
    {
        get
        {
            lock (_demoSync)
            {
                if (!_isDemoModeEnabled || _demoLocation is null)
                    return null;

                return new Location(_demoLocation.Latitude, _demoLocation.Longitude);
            }
        }
    }

    public async Task<Location?> GetLastKnownAsync(CancellationToken ct = default)
    {
        var demo = DemoLocation;
        if (demo is not null)
            return demo;

        try
        {
            return await Geolocation.Default.GetLastKnownLocationAsync();
        }
        catch
        {
            return null;
        }
    }

    public async Task<Location?> GetCurrentAsync(
        LocationTrackingProfile profile,
        CancellationToken ct = default)
    {
        var demo = DemoLocation;
        if (demo is not null)
            return demo;

        try
        {
            var request = new GeolocationRequest(profile.CurrentAccuracy, profile.CurrentTimeout);
            return await Geolocation.Default.GetLocationAsync(request, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> StartListeningAsync(
        LocationTrackingProfile profile,
        CancellationToken ct = default)
    {
        if (_isListening && Equals(_activeProfile, profile))
            return true;

        if (_isListening)
            StopListening();

        try
        {
            Geolocation.LocationChanged -= OnGeolocationChanged;
            Geolocation.LocationChanged += OnGeolocationChanged;

            var request = new GeolocationListeningRequest(profile.ListeningAccuracy);
            var started = await Geolocation.StartListeningForegroundAsync(request);

            _isListening = started;
            _activeProfile = started ? profile : null;
            return started;
        }
        catch
        {
            _activeProfile = null;
            return false;
        }
    }

    public void StopListening()
    {
        if (!_isListening)
            return;

        try
        {
            Geolocation.LocationChanged -= OnGeolocationChanged;
            Geolocation.StopListeningForeground();
        }
        catch
        {
        }
        finally
        {
            _isListening = false;
            _activeProfile = null;
        }
    }

    public void EnableDemoMode(double latitude, double longitude)
    {
        Location demoLocation;

        lock (_demoSync)
        {
            _isDemoModeEnabled = true;
            _demoLocation = new Location(latitude, longitude);
            demoLocation = _demoLocation;
        }

        LocationChanged?.Invoke(this, demoLocation);
    }

    public void DisableDemoMode()
    {
        lock (_demoSync)
        {
            _isDemoModeEnabled = false;
            _demoLocation = null;
        }
    }

    public void SimulateLocation(double latitude, double longitude)
    {
        EnableDemoMode(latitude, longitude);
    }

    private void OnGeolocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        if (IsDemoModeEnabled)
            return;

        LocationChanged?.Invoke(this, e.Location);
    }
}