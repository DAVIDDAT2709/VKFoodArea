using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using VKFoodArea.Helpers;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace VKFoodArea
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "https",
        DataHost = AppLinkConstants.Host,
        DataPathPrefix = AppLinkConstants.QrPathPrefix,
        AutoVerify = true)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = AppLinkConstants.CustomScheme,
        DataHost = AppLinkConstants.CustomSchemeHost)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ForwardAppLink(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
            ForwardAppLink(intent);
        }

        private static void ForwardAppLink(Intent? intent)
        {
            var data = intent?.DataString;
            if (string.IsNullOrWhiteSpace(data) ||
                !Uri.TryCreate(data, UriKind.Absolute, out var uri))
            {
                return;
            }

            if (MauiApplication.Current is App app)
            {
                app.ReceiveAppLink(uri);
                return;
            }

            PendingAppLinkStore.Store(uri);
        }
    }
}
