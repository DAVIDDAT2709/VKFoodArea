using System.Diagnostics;

#if ANDROID
using Android.Util;
#endif

namespace VKFoodArea.Helpers;

internal static class AppStartupTrace
{
    public static void Log(string message)
    {
#if DEBUG
        var payload = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(payload);
#if ANDROID
        Logcat(payload);
#endif
#endif
    }

#if DEBUG && ANDROID
    private static void Logcat(string message)
        => Android.Util.Log.Info("VKFoodArea.Startup", message);
#endif
}
