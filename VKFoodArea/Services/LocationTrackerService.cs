using Microsoft.Maui.Devices.Sensors;

namespace VKFoodArea.Services;

public class LocationTrackerService
{
    public event EventHandler<Location>? LocationChanged;

    private bool _isListening;

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

    public async Task<Location?> GetCurrentAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            return await Geolocation.Default.GetLocationAsync(request, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> StartListeningAsync(CancellationToken ct = default)
    {
        if (_isListening)
            return true;

        try
        {
            Geolocation.LocationChanged += OnGeolocationChanged;

            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best);
            var started = await Geolocation.StartListeningForegroundAsync(request);

            _isListening = started;
            return started;
        }
        catch
        {
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