using Microsoft.Maui.Storage;

namespace VKFoodArea.Services;

public sealed class DeviceIdentityService
{
    private const string DeviceKeyPreference = "presence_device_key";

    public string GetOrCreateDeviceKey()
    {
        var existing = Normalize(Preferences.Default.Get(DeviceKeyPreference, string.Empty));
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var created = $"{DeviceInfo.Current.Platform.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
        Preferences.Default.Set(DeviceKeyPreference, created);
        return created;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}