using VKFoodArea.Services;
using VKFoodArea.Features.Home;

namespace VKFoodArea.Features.Startup;

public partial class StartupPage : ContentPage
{
    private readonly AppDbInitializationService _dbInitializationService;
    private readonly AuthService _authService;
    private readonly AppSettingsService _settingsService;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly AppTextService _text;
    private bool _started;

    public StartupPage(
        AppDbInitializationService dbInitializationService,
        AuthService authService,
        AppSettingsService settingsService,
        AppRootNavigationService rootNavigationService,
        AppTextService text)
    {
        InitializeComponent();
        _dbInitializationService = dbInitializationService;
        _authService = authService;
        _settingsService = settingsService;
        _rootNavigationService = rootNavigationService;
        _text = text;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
            return;

        _started = true;

        try
        {
            await _dbInitializationService.EnsureInitializedAsync();
            var restoredSession = await _authService.TryRestoreSessionAsync();

            if (restoredSession || _settingsService.HasCompletedEntryFlow)
            {
                await _rootNavigationService.SetRootAsync<HomeDesignPage>();
                return;
            }

            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                _text["Common.Error"],
                FriendlyErrorMessages.Get(ex, _text, FriendlyErrorContext.Startup),
                _text["Common.Ok"]);

            if (_settingsService.HasCompletedEntryFlow)
            {
                await _rootNavigationService.SetRootAsync<HomeDesignPage>();
                return;
            }

            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
    }
}
