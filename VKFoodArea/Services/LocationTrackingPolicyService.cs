using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;

namespace VKFoodArea.Services;

public enum LocationTrackingMode
{
    ForegroundNavigation,
    BackgroundMonitoring
}

public enum LocationPermissionScope
{
    Foreground,
    Background
}

public sealed record LocationTrackingProfile(
    LocationTrackingMode Mode,
    LocationPermissionScope PermissionScope,
    GeolocationAccuracy CurrentAccuracy,
    GeolocationAccuracy ListeningAccuracy,
    TimeSpan CurrentTimeout,
    string PolicyName);

public class LocationTrackingPolicyService
{
    private readonly object _sync = new();
    private bool _isAppForeground = true;

    public event EventHandler? ProfileChanged;

    public LocationTrackingPolicyService()
    {
        Battery.Default.EnergySaverStatusChanged += OnEnergySaverStatusChanged;
    }

    public LocationTrackingProfile GetCurrentProfile()
    {
        lock (_sync)
        {
            return BuildProfile(_isAppForeground, Battery.Default.EnergySaverStatus);
        }
    }

    public void SetAppForeground(bool isAppForeground)
    {
        var changed = false;

        lock (_sync)
        {
            if (_isAppForeground == isAppForeground)
                return;

            _isAppForeground = isAppForeground;
            changed = true;
        }

        if (changed)
            ProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEnergySaverStatusChanged(object? sender, EnergySaverStatusChangedEventArgs e)
        => ProfileChanged?.Invoke(this, EventArgs.Empty);

    private static LocationTrackingProfile BuildProfile(
        bool isAppForeground,
        EnergySaverStatus energySaverStatus)
    {
        var saverOn = energySaverStatus == EnergySaverStatus.On;

        if (isAppForeground)
        {
            return new LocationTrackingProfile(
                LocationTrackingMode.ForegroundNavigation,
                LocationPermissionScope.Foreground,
                saverOn ? GeolocationAccuracy.High : GeolocationAccuracy.Best,
                saverOn ? GeolocationAccuracy.High : GeolocationAccuracy.Best,
                saverOn ? TimeSpan.FromSeconds(12) : TimeSpan.FromSeconds(8),
                saverOn ? "foreground-saver" : "foreground-active");
        }

        return new LocationTrackingProfile(
            LocationTrackingMode.BackgroundMonitoring,
            LocationPermissionScope.Background,
            saverOn ? GeolocationAccuracy.Low : GeolocationAccuracy.Medium,
            saverOn ? GeolocationAccuracy.Low : GeolocationAccuracy.Medium,
            saverOn ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(20),
            saverOn ? "background-saver" : "background-monitor");
    }
}
