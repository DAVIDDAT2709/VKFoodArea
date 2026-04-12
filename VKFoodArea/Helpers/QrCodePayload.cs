namespace VKFoodArea.Helpers;

public static class QrCodePayload
{
    public static string Normalize(string? value)
    {
        var normalized = Extract(value).Trim().ToLowerInvariant();
        return normalized;
    }

    public static string Extract(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return normalized;

        var query = uri.Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                parts[0].Equals("code", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (segments.Length == 0)
            return normalized;

        var qrIndex = Array.FindIndex(
            segments,
            x => x.Equals("qr", StringComparison.OrdinalIgnoreCase));

        if (qrIndex >= 0 && qrIndex < segments.Length - 1)
            return segments[qrIndex + 1];

        return segments[^1];
    }
}
