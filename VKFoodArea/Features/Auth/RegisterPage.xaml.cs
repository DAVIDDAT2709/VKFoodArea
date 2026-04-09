using VKFoodArea.Services;

namespace VKFoodArea.Features.Auth;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly AppTextService _text;

    public RegisterPage(AuthService authService, AppTextService text)
    {
        InitializeComponent();
        _authService = authService;
        _text = text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();
    }

    private void OnFullNameCompleted(object sender, EventArgs e)
    {
        EmailEntry.Focus();
    }

    private void OnEmailCompleted(object sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    private void OnPasswordCompleted(object sender, EventArgs e)
    {
        ConfirmPasswordEntry.Focus();
    }

    private async void OnConfirmPasswordCompleted(object sender, EventArgs e)
    {
        await HandleRegisterAsync();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await HandleRegisterAsync();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async Task HandleRegisterAsync()
    {
        MessageLabel.Text = string.Empty;

        var fullName = FullNameEntry.Text?.Trim() ?? string.Empty;
        var email = EmailEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            MessageLabel.Text = _text["Register.RequiredError"];
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            MessageLabel.Text = _text["Register.PasswordMismatchError"];
            return;
        }

        var result = await _authService.RegisterAsync(fullName, email, password);
        if (!result.IsSuccess)
        {
            MessageLabel.Text = _text[result.ErrorKey ?? "Register.FailedError"];
            return;
        }

        await DisplayAlert(
            _text["Register.SuccessTitle"],
            _text["Register.SuccessMessage"],
            _text["Common.Ok"]);

        await Navigation.PopAsync();
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Register.PageTitle"];
        BackButton.Text = _text["Register.BackShort"];
        RegisterHeroLabel.Text = _text["Register.Hero"];
        RegisterTitleLabel.Text = _text["Register.Title"];
        RegisterSubtitleLabel.Text = _text["Register.Subtitle"];
        FullNameLabel.Text = _text["Register.FullNameLabel"];
        FullNameEntry.Placeholder = _text["Register.FullNamePlaceholder"];
        EmailLabel.Text = _text["Register.EmailLabel"];
        EmailEntry.Placeholder = _text["Register.EmailPlaceholder"];
        PasswordLabel.Text = _text["Register.PasswordLabel"];
        PasswordEntry.Placeholder = _text["Register.PasswordPlaceholder"];
        ConfirmPasswordLabel.Text = _text["Register.ConfirmPasswordLabel"];
        ConfirmPasswordEntry.Placeholder = _text["Register.ConfirmPasswordPlaceholder"];
        RegisterButton.Text = _text["Register.Submit"];
        BackToLoginButton.Text = _text["Register.BackToLogin"];
    }
}
