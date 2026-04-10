using Microsoft.Maui.Devices.Sensors;

namespace VKFoodArea.Services;

public class LocationTrackerService
{
    public event EventHandler<Location>? LocationChanged;

    private bool _isListening;
    private LocationTrackingProfile? _activeProfile;

    public async Task<Location?> GetLastKnownAsync(CancellationToken ct = default)
    {
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

    private void OnGeolocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        LocationChanged?.Invoke(this, e.Location);
    }

    public void SimulateLocation(double latitude, double longitude)
    {
        LocationChanged?.Invoke(this, new Location(latitude, longitude));
    }
}
