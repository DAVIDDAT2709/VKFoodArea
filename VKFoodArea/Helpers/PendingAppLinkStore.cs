namespace VKFoodArea.Helpers;

public static class PendingAppLinkStore
{
    private static readonly object Sync = new();
    private static Uri? _pendingUri;

    public static void Store(Uri uri)
    {
        lock (Sync)
        {
            _pendingUri = uri;
        }
    }

    public static Uri? Take()
    {
        lock (Sync)
        {
            var uri = _pendingUri;
            _pendingUri = null;
            return uri;
        }
    }
}
