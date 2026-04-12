using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Auth;
using VKFoodArea.Features.Home;
using VKFoodArea.Features.Settings;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.User;

public partial class UserPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly AppLanguageService _languageService;
    private readonly AppSettingsService _settingsService;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;

    public UserPage(
        AuthService authService,
        AppLanguageService languageService,
        AppSettingsService settingsService,
        AppRootNavigationService rootNavigationService,
        IServiceProvider serviceProvider,
        AppTextService text)
    {
        InitializeComponent();
        _authService = authService;
        _languageService = languageService;
        _settingsService = settingsService;
        _rootNavigationService = rootNavigationService;
        _serviceProvider = serviceProvider;
        _text = text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();
        RefreshUserInfo();
    }

    private void RefreshUserInfo()
    {
        var user = _authService.CurrentUser;
        var fullName = ResolveDisplayName(user);
        var currentLanguage = AppLanguageService.NormalizeLanguage(_languageService.CurrentLanguage);
        var currentMode = NormalizeNarrationMode(_settingsService.NarrationOutputMode);
        var isLoggedIn = user is not null;
        var isActive = user?.IsActive ?? false;

        AvatarLabel.Text = BuildInitials(fullName);
        FullNameLabel.Text = fullName;
        UsernameLabel.Text = user is null
            ? _text["User.NoAccount"]
            : $"@{user.Username}";

        RoleBadgeLabel.Text = GetRoleDisplayLocalized(user?.Role);
        StatusBadgeLabel.Text = !isLoggedIn
            ? _text["User.Guest"]
            : isActive ? _text["User.Active"] : _text["User.Disabled"];

        CurrentLanguageValueLabel.Text = _text.GetLanguageDisplay(currentLanguage);
        CurrentLanguageHintLabel.Text = _text.GetUserTypeDisplay(_languageService.UserType);
        UserTypeValueLabel.Text = _text.GetUserTypeDisplay(_languageService.UserType);
        NarrationModeValueLabel.Text = _text.GetModeDisplay(currentMode);

        UsernameValueLabel.Text = user?.Username ?? "--";
        RoleValueLabel.Text = GetRoleDisplayLocalized(user?.Role);
        StatusValueLabel.Text = !isLoggedIn
            ? _text["User.NotLoggedIn"]
            : isActive ? _text["User.Ready"] : _text["User.Locked"];
        FooterNoteLabel.Text = _text["User.FooterNote"];
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnOpenProfileClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<AccountProfilePage>();
        await Navigation.PushAsync(page);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            _text["User.LogoutTitle"],
            _text["User.LogoutMessage"],
            _text["User.LogoutConfirm"],
            _text["Common.Cancel"]);

        if (!confirmed)
            return;

        _authService.Logout();
        await _rootNavigationService.SetRootAsync<LoginPage>();
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.FirstOrDefault() is HomeDesignPage)
        {
            await Navigation.PopToRootAsync();
            return;
        }

        await _rootNavigationService.SetRootAsync<HomeDesignPage>();
    }

    private async void OnOpenFullMapClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count >= 2 &&
            Navigation.NavigationStack[^2] is FullMapPage)
        {
            await Navigation.PopAsync();
            return;
        }

        var page = _serviceProvider.GetRequiredService<FullMapPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<HistoryPage>();
        await Navigation.PushAsync(page);
    }

    private void OnUserClicked(object sender, EventArgs e)
    {
        // Current page.
    }

    private static string ResolveDisplayName(AppUser? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.FullName))
            return user.FullName;

        if (!string.IsNullOrWhiteSpace(user?.Username))
            return user.Username;

        return "Kh\u00E1ch VKFood";
    }

    private static string NormalizeNarrationMode(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode)
            ? "TTS"
            : mode.Trim();
    }

    private static string BuildInitials(string fullName)
    {
        var tokens = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(token => char.ToUpperInvariant(token[0]))
            .ToArray();

        return tokens.Length == 0
            ? "VK"
            : new string(tokens);
    }

    private string GetRoleDisplayLocalized(string? role)
    {
        return _text.CurrentLanguage switch
        {
            "en" => role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "Administrator",
                "user" => "User",
                _ => _text["User.Guest"]
            },
            "zh" => role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "管理员",
                "user" => "用户",
                _ => _text["User.Guest"]
            },
            "ja" => role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "管理者",
                "user" => "利用者",
                _ => _text["User.Guest"]
            },
            "de" => role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "Administrator",
                "user" => "Benutzer",
                _ => _text["User.Guest"]
            },
            _ => role?.Trim().ToLowerInvariant() switch
            {
                "admin" => "Quản trị viên",
                "user" => "Người dùng",
                _ => _text["User.Guest"]
            }
        };
    }

    private void ApplyLocalizedText()
    {
        Title = _text["User.PageTitle"];
        HeaderTagLabel.Text = _text["User.HeaderTag"];
        CurrentLanguageTitleLabel.Text = _text["User.Language"];
        UserTypeTitleLabel.Text = _text["User.UserType"];
        AccountInfoTitleLabel.Text = _text["User.AccountInfo"];
        UsernameTitleLabel.Text = _text["User.Username"];
        RoleTitleLabel.Text = _text["User.Role"];
        StatusTitleLabel.Text = _text["User.Status"];
        QuickActionsTitleLabel.Text = _text["User.QuickActions"];
        OpenSettingsButton.Text = _text["User.SoundSettings"];
        LogoutButton.Text = _text["User.Logout"];
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }
}
