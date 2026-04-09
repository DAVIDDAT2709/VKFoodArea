using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Auth;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly LanguageSelectionFlowService _languageSelectionFlowService;
    private readonly AppTextService _text;
    private string _selectedUserType = "domestic";
    private string _selectedLanguage = "en";

    public LoginPage(
        AuthService authService,
        IServiceProvider serviceProvider,
        LanguageSelectionFlowService languageSelectionFlowService,
        AppTextService text)
    {
        InitializeComponent();
        _authService = authService;
        _serviceProvider = serviceProvider;
        _languageSelectionFlowService = languageSelectionFlowService;
        _text = text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();
        BuildLanguageButtons();
        ApplySelectionVisuals();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await HandleLoginAsync();
    }

    private void OnUsernameCompleted(object sender, EventArgs e)
    {
        PasswordEntry.Focus();
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await HandleLoginAsync();
    }

    private async Task HandleLoginAsync()
    {
        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        MessageLabel.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageLabel.Text = _text["Login.RequiredError"];
            return;
        }

        var success = await _authService.LoginAsync(username, password);

        if (!success)
        {
            MessageLabel.Text = _text["Login.InvalidError"];
            return;
        }

        SelectionOverlay.IsVisible = true;
    }

    private void OnDomesticSelected(object sender, TappedEventArgs e)
    {
        _selectedUserType = "domestic";
        _selectedLanguage = "vi";
        ApplySelectionVisuals();
    }

    private void OnTouristSelected(object sender, TappedEventArgs e)
    {
        _selectedUserType = "tourist";

        if (_selectedLanguage == "vi")
            _selectedLanguage = "en";

        ApplySelectionVisuals();
    }

    private void OnSelectionBackClicked(object sender, EventArgs e)
    {
        SelectionOverlay.IsVisible = false;
    }

    private void OnSelectionConfirmClicked(object sender, EventArgs e)
    {
        if (_selectedUserType == "domestic")
            _languageSelectionFlowService.ApplyDomestic();
        else
            _languageSelectionFlowService.ApplyTourist(_selectedLanguage);

        SelectionOverlay.IsVisible = false;
        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<HomeDesignPage>());
    }

    private void OnLanguageButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string language)
            return;

        _selectedLanguage = language;
        ApplySelectionVisuals();
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Login.PageTitle"];
        LoginTitleLabel.Text = _text["Login.Title"];
        UsernameLabel.Text = _text["Login.UsernameLabel"];
        UsernameEntry.Placeholder = _text["Login.UsernamePlaceholder"];
        PasswordLabel.Text = _text["Login.PasswordLabel"];
        PasswordEntry.Placeholder = _text["Login.PasswordPlaceholder"];
        LoginButton.Text = _text["Login.Submit"];
        SelectionTitleLabel.Text = _text["Login.SelectionTitle"];
        SelectionSubtitleLabel.Text = _text["Login.SelectionSubtitle"];
        DomesticTitleLabel.Text = _text["Login.DomesticTitle"];
        DomesticDescriptionLabel.Text = _text["Login.DomesticDescription"];
        TouristTitleLabel.Text = _text["Login.TouristTitle"];
        TouristDescriptionLabel.Text = _text["Login.TouristDescription"];
        LanguageTitleLabel.Text = _text["Login.LanguageTitle"];
        SelectionBackButton.Text = _text["Login.BackButton"];
        SelectionConfirmButton.Text = _text["Login.EnterButton"];
    }

    private void BuildLanguageButtons()
    {
        if (LanguageOptionsLayout.Children.Count > 0)
            return;

        foreach (var option in new[]
                 {
                     ("English", "en"),
                     ("中文", "zh"),
                     ("日本語", "ja"),
                     ("Deutsch", "de")
                 })
        {
            var button = new Button
            {
                Text = option.Item1,
                CommandParameter = option.Item2,
                BackgroundColor = Color.FromArgb("#F2F6F5"),
                TextColor = Color.FromArgb("#35534D"),
                CornerRadius = 16,
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                Padding = new Thickness(14, 10),
                Margin = new Thickness(0, 0, 10, 10)
            };

            button.Clicked += OnLanguageButtonClicked;
            LanguageOptionsLayout.Children.Add(button);
        }
    }

    private void ApplySelectionVisuals()
    {
        var isDomestic = _selectedUserType == "domestic";

        DomesticOptionBorder.BackgroundColor = isDomestic
            ? Color.FromArgb("#E8F7F2")
            : Color.FromArgb("#F3FAF7");
        DomesticOptionBorder.Stroke = isDomestic
            ? Color.FromArgb("#129488")
            : Color.FromArgb("#CFE3DD");

        TouristOptionBorder.BackgroundColor = !isDomestic
            ? Color.FromArgb("#FFF1D8")
            : Color.FromArgb("#F8F6EC");
        TouristOptionBorder.Stroke = !isDomestic
            ? Color.FromArgb("#D59C29")
            : Color.FromArgb("#E4DAC2");

        LanguageSelectionContainer.IsVisible = !isDomestic;

        foreach (var child in LanguageOptionsLayout.Children.OfType<Button>())
        {
            var isActive = string.Equals(child.CommandParameter?.ToString(), _selectedLanguage, StringComparison.Ordinal);
            child.BackgroundColor = isActive
                ? Color.FromArgb("#183A35")
                : Color.FromArgb("#F2F6F5");
            child.TextColor = isActive
                ? Colors.White
                : Color.FromArgb("#35534D");
        }
    }
}
