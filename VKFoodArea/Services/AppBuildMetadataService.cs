using System.Reflection;

namespace VKFoodArea.Services;

public sealed class AppBuildMetadataService
{
    private readonly IReadOnlyDictionary<string, string> _metadata;

    public AppBuildMetadataService()
    {
        _metadata = typeof(AppBuildMetadataService)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Last().Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    public string OfficialBaseUrl => NormalizeBaseUrl(GetMetadata("VKFoodAreaOfficialBaseUrl"));

    public bool HasOfficialBaseUrl => !string.IsNullOrWhiteSpace(OfficialBaseUrl);

    public bool DemoToolsEnabled => bool.TryParse(GetMetadata("VKFoodAreaEnableInternalDemo"), out var enabled) && enabled;

    private string GetMetadata(string key)
        => _metadata.TryGetValue(key, out var value)
            ? value
            : string.Empty;

    private static string NormalizeBaseUrl(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var authorityOnly = uri.GetLeftPart(UriPartial.Authority);
            return authorityOnly.EndsWith('/')
                ? authorityOnly
                : $"{authorityOnly}/";
        }

        return normalized.EndsWith('/')
            ? normalized
            : $"{normalized}/";
    }
}
