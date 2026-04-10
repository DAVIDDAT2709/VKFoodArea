using Microsoft.Maui.ApplicationModel;

namespace VKFoodArea.Services;

public class PermissionService
{
    public Task<bool> EnsureLocationPermissionAsync(
        LocationTrackingProfile profile,
        bool requestIfNeeded = true)
        => EnsureLocationPermissionAsync(profile.PermissionScope, requestIfNeeded);

    public async Task<bool> EnsureLocationPermissionAsync(
        LocationPermissionScope permissionScope,
        bool requestIfNeeded = true)
    {
        try
        {
            var hasForegroundPermission = await EnsurePermissionAsync<Permissions.LocationWhenInUse>(requestIfNeeded);
            if (!hasForegroundPermission)
                return false;

            if (permissionScope != LocationPermissionScope.Background)
                return true;

            var hasBackgroundPermission = await EnsurePermissionAsync<Permissions.LocationAlways>(requestIfNeeded);
            return hasBackgroundPermission || hasForegroundPermission;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> EnsurePermissionAsync<TPermission>(bool requestIfNeeded)
        where TPermission : Permissions.BasePermission, new()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<TPermission>();

            if (status != PermissionStatus.Granted && requestIfNeeded)
                status = await Permissions.RequestAsync<TPermission>();

            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }
}
