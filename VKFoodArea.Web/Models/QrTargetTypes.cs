namespace VKFoodArea.Web.Models;

public static class QrTargetTypes
{
    public const string Poi = "poi";
    public const string Tour = "tour";

    public static string Normalize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            Tour => Tour,
            _ => Poi
        };
    }

    public static bool IsValid(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is Poi or Tour;
    }

    public static string ToDisplayLabel(string? value)
        => Normalize(value) == Tour ? "Tour" : "POI";
}
