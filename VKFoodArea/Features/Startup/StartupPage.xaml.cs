using VKFoodArea.Services;
using VKFoodArea.Features.Home;
using VKFoodArea.Helpers;

namespace VKFoodArea.Features.Startup;

public partial class StartupPage : ContentPage
{
    private const string StartupErrorTitle = "Startup error";
    private const string StartupErrorButton = "OK";

    private readonly AppDbInitializationService _dbInitializationService;
    private readonly AuthService _authService;
    private readonly AppSettingsService _settingsService;
    private readonly AppRootNavigationService _rootNavigationService;
    private bool _started;

    public StartupPage(
        AppDbInitializationService dbInitializationService,
        AuthService authService,
        AppSettingsService settingsService,
        AppRootNavigationService rootNavigationService)
    {
        InitializeComponent();
        _dbInitializationService = dbInitializationService;
        _authService = authService;
        _settingsService = settingsService;
        _rootNavigationService = rootNavigationService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AppStartupTrace.Log("StartupPage.OnAppearing");

        if (_started)
            return;

        _started = true;

        try
        {
            await Task.Yield();
            AppStartupTrace.Log("StartupPage database init start");
            await _dbInitializationService.EnsureInitializedAsync();
            AppStartupTrace.Log("StartupPage database init complete");
            AppStartupTrace.Log("StartupPage session restore start");
            var restoredSession = await _authService.TryRestoreSessionAsync();
            AppStartupTrace.Log($"StartupPage session restore complete: {restoredSession}");

            if (restoredSession || _settingsService.HasCompletedEntryFlow)
            {
                AppStartupTrace.Log("StartupPage navigating to HomeDesignPage");
                await _rootNavigationService.SetRootAsync<HomeDesignPage>();
                return;
            }

            AppStartupTrace.Log("StartupPage navigating to HomeEntryPage");
            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
        catch (Exception ex)
        {
            AppStartupTrace.Log($"StartupPage failed: {ex.GetType().Name}: {ex.Message}");
            await DisplayAlertAsync(
                StartupErrorTitle,
                ex.Message,
                StartupErrorButton);

            if (_settingsService.HasCompletedEntryFlow)
            {
                await _rootNavigationService.SetRootAsync<HomeDesignPage>();
                return;
            }

            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
    }
}
