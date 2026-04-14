using Microsoft.Maui.Storage;

namespace VKFoodArea.Services;

public sealed class AnonymousIdentityService
{
    private const string AnonymousUserKeyPreference = "anonymous_sync_key";

    public string GetOrCreateAnonymousUserKey()
    {
        var existingKey = Normalize(Preferences.Default.Get(AnonymousUserKeyPreference, string.Empty));
        if (!string.IsNullOrWhiteSpace(existingKey))
            return existingKey;

        var createdKey = $"guest-{Guid.NewGuid():N}";
        Preferences.Default.Set(AnonymousUserKeyPreference, createdKey);
        return createdKey;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}
