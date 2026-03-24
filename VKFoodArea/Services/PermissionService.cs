using Microsoft.Maui.ApplicationModel;

namespace VKFoodArea.Services;

public class PermissionService
{
    public async Task<bool> EnsureLocationPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            return status == PermissionStatus.Granted;
        }
        catch
        {
            return false;
        }
    }
}