using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;
using VKFoodArea.Data;
using VKFoodArea.Features.Home;
using VKFoodArea.Features.Settings;
using VKFoodArea.Features.Startup;
using VKFoodArea.Features.User;
using VKFoodArea.Repositories;
using VKFoodArea.Services;
using Microsoft.Extensions.DependencyInjection;
namespace VKFoodArea;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseBarcodeReader()
            .AddAudio()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "vkfoodarea.db");

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddSingleton<HaversineDistanceCalculator>();
        builder.Services.AddSingleton<CooldownStore>();
        builder.Services.AddSingleton<GeofenceEngine>();
        builder.Services.AddSingleton<LocationTrackerService>();
        builder.Services.AddSingleton<LocationTrackingPolicyService>();
        builder.Services.AddSingleton<PermissionService>();
        builder.Services.AddSingleton<PoiRuntimeService>();
        builder.Services.AddSingleton<TourSessionService>();

        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<AppBuildMetadataService>();
        builder.Services.AddSingleton<AppLanguageService>();
        builder.Services.AddSingleton<AppTextService>();
        builder.Services.AddSingleton<AppRootNavigationService>();
        builder.Services.AddSingleton<LanguageSelectionFlowService>();
        builder.Services.AddSingleton<NarrationUiStateService>();
        builder.Services.AddSingleton<SessionStoreService>();
        builder.Services.AddSingleton<AnonymousIdentityService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<AppAuthorizationService>();
        builder.Services.AddSingleton<ApiBaseUrlService>();
        builder.Services.AddSingleton<AppSyncOutboxService>();
        builder.Services.AddSingleton<AppLinkService>();
        builder.Services.AddSingleton<AppDbInitializationService>();

        builder.Services.AddHttpClient(AppRemoteHttpClientNames.Primary, ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<QrLookupService>(ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<NarrationSyncService>(ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<AppUserSyncService>(ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<MovementLogSyncService>(ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<PoiSyncService>(ConfigureRemoteHttpClient);
        builder.Services.AddHttpClient<TourCatalogService>(ConfigureRemoteHttpClient);

        builder.Services.AddTransient<NarrationService>();
        builder.Services.AddTransient<TourNarrationService>();
        builder.Services.AddTransient<PoiAudioCacheService>();
        builder.Services.AddTransient<AccountService>();
        builder.Services.AddTransient<HistoryService>();
        builder.Services.AddTransient<PoiService>();
        builder.Services.AddTransient<SoundSettingsService>();
        builder.Services.AddTransient<TtsAudioPreviewService>();
        builder.Services.AddTransient<PoiRepository>();
        builder.Services.AddTransient<FoodRepository>();

        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<AccountSettingsViewModel>();
        builder.Services.AddTransient<SoundSettingsViewModel>();

        builder.Services.AddTransient<HomeDesignPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<FullMapPage>();
        builder.Services.AddTransient<PoiDetailPage>();
        builder.Services.AddTransient<QrScannerPage>();
        builder.Services.AddTransient<TourCatalogPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AccountProfilePage>();
        builder.Services.AddTransient<TourSessionPage>();
        builder.Services.AddTransient<UserPage>();
        builder.Services.AddTransient<HomeEntryPage>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddSingleton<DeviceIdentityService>();
        builder.Services.AddHttpClient<AppDevicePresenceService>(ConfigureRemoteHttpClient);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void ConfigureRemoteHttpClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(8);
        client.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");
    }
}
