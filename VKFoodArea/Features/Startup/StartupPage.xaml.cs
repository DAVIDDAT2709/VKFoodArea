using VKFoodArea.Services;

namespace VKFoodArea.Features.Startup;

public partial class StartupPage : ContentPage
{
    private readonly AppDbInitializationService _dbInitializationService;
    private readonly AuthService _authService;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly AppTextService _text;
    private bool _started;

    public StartupPage(
        AppDbInitializationService dbInitializationService,
        AuthService authService,
        AppRootNavigationService rootNavigationService,
        AppTextService text)
    {
        InitializeComponent();
        _dbInitializationService = dbInitializationService;
        _authService = authService;
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
            _authService.Logout();
            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_text["Common.Error"], ex.Message, _text["Common.Ok"]);
            _authService.Logout();
            await _rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
    }
}
