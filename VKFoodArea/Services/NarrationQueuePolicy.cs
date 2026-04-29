namespace VKFoodArea.Services;

public static class NarrationQueuePolicy
{
    public const string QueueScope = "device-local";

    public static bool ShouldQueuePlayback(string? triggerSource)
        => (triggerSource ?? string.Empty).Trim().ToLowerInvariant() is "auto" or "gps" or "tour" or "qr";
}
