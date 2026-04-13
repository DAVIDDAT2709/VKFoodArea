namespace VKFoodArea.Helpers;

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
}
