using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;
using VKFoodArea.Data;
using VKFoodArea.Features.Auth;
using VKFoodArea.Features.Home;
using VKFoodArea.Features.Settings;
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

        builder.Services.AddSingleton<HaversineDistanceCalculator>();
        builder.Services.AddSingleton<CooldownStore>();
        builder.Services.AddSingleton<GeofenceEngine>();
        builder.Services.AddSingleton<LocationTrackerService>();
        builder.Services.AddSingleton<PermissionService>();

        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<AppLanguageService>();
        builder.Services.AddSingleton<AppTextService>();
        builder.Services.AddSingleton<LanguageSelectionFlowService>();
        builder.Services.AddSingleton<ApiBaseUrlService>();
        builder.Services.AddSingleton<NarrationUiStateService>();
        builder.Services.AddSingleton<SessionStoreService>();

        builder.Services.AddHttpClient<QrLookupService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });

        builder.Services.AddHttpClient<NarrationSyncService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });

        builder.Services.AddHttpClient<PoiSyncService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
        });

        builder.Services.AddTransient<NarrationService>();
        builder.Services.AddTransient<AccountService>();
        builder.Services.AddTransient<HistoryService>();
        builder.Services.AddTransient<PoiService>();
        builder.Services.AddTransient<SoundSettingsService>();
        builder.Services.AddTransient<TtsAudioPreviewService>();
        builder.Services.AddTransient<PoiRepository>();
        builder.Services.AddTransient<FoodRepository>();
        builder.Services.AddSingleton<AuthService>();

        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<AccountSettingsViewModel>();
        builder.Services.AddTransient<SoundSettingsViewModel>();

        builder.Services.AddTransient<HomeDesignPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<FullMapPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<PoiDetailPage>();
        builder.Services.AddTransient<QrScannerPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<UserPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppDataInitializer.InitializeAsync(db).GetAwaiter().GetResult();

        return app;
    }
}
