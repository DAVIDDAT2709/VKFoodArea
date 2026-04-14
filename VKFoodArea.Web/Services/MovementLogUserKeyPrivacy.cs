using System.Security.Cryptography;
using System.Text;

namespace VKFoodArea.Web.Services;

public static class MovementLogUserKeyPrivacy
{
    private const string AnonymousPrefix = "anon_";

    public static string NormalizeForStorage(string? userKey)
    {
        var normalized = NormalizeRaw(userKey);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (IsAnonymousKey(normalized))
            return normalized;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{AnonymousPrefix}{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }

    public static bool IsAnonymousKey(string? userKey)
    {
        var normalized = NormalizeRaw(userKey);
        if (!normalized.StartsWith(AnonymousPrefix, StringComparison.Ordinal))
            return false;

        var hash = normalized[AnonymousPrefix.Length..];
        return hash.Length == 64 && hash.All(IsHexDigit);
    }

    private static string NormalizeRaw(string? userKey)
        => (userKey ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsHexDigit(char value)
        => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
}
