using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Auth;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly LanguageSelectionFlowService _languageSelectionFlowService;

    public LoginPage(
        AuthService authService,
        IServiceProvider serviceProvider,
        LanguageSelectionFlowService languageSelectionFlowService)
    {
        InitializeComponent();
        _authService = authService;
        _serviceProvider = serviceProvider;
        _languageSelectionFlowService = languageSelectionFlowService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        var success = await _authService.LoginAsync(username, password);

        if (!success)
        {
            MessageLabel.Text = "Sai tài khoản hoặc mật khẩu.";
            return;
        }
        await _languageSelectionFlowService.ShowLanguageSelectionAsync(this);

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<HomeDesignPage>());
    }
}