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
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;

    public UserPage(
        AuthService authService,
        AppLanguageService languageService,
        AppSettingsService settingsService,
        IServiceProvider serviceProvider,
        AppTextService text)
    {
        InitializeComponent();
        _authService = authService;
        _languageService = languageService;
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;
        _text = text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedTextClean();
        RefreshUserInfoEscaped();
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
            ? "Chưa có tài khoản đang đăng nhập"
            : $"@{user.Username}";

        RoleBadgeLabel.Text = GetRoleDisplay(user?.Role);
        StatusBadgeLabel.Text = !isLoggedIn
            ? "Khách"
            : isActive ? "Đang hoạt động" : "Tạm khóa";

        CurrentLanguageValueLabel.Text = GetLanguageDisplay(currentLanguage);
        CurrentLanguageHintLabel.Text = $"{GetUserTypeDisplay(_languageService.UserType)} đang dùng";
        UserTypeValueLabel.Text = GetUserTypeDisplay(_languageService.UserType);
        NarrationModeValueLabel.Text = $"Chế độ phát: {GetModeDisplay(currentMode)}";

        UsernameValueLabel.Text = user?.Username ?? "--";
        RoleValueLabel.Text = GetRoleDisplay(user?.Role);
        StatusValueLabel.Text = !isLoggedIn
            ? "Chưa đăng nhập"
            : isActive ? "Sẵn sàng sử dụng" : "Tài khoản đang bị khóa";
        FooterNoteLabel.Text = "Đăng xuất sẽ đưa bạn về màn hình đăng nhập và chọn lại ngôn ngữ khi vào app.";
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Đăng xuất",
            "Bạn có muốn đăng xuất khỏi tài khoản hiện tại không?",
            "Đăng xuất",
            "Hủy");

        if (!confirmed)
            return;

        _authService.Logout();

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.OfType<HomeDesignPage>().Any())
        {
            await Navigation.PopToRootAsync();
            return;
        }

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<HomeDesignPage>());
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

    private void RefreshUserInfoEscaped()
    {
        var user = _authService.CurrentUser;
        var fullName = ResolveDisplayNameEscaped(user);
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

    private async void OnLogoutClickedEscaped(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            _text["User.LogoutTitle"],
            _text["User.LogoutMessage"],
            _text["User.LogoutConfirm"],
            _text["Common.Cancel"]);

        if (!confirmed)
            return;

        _authService.Logout();

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
    }

    private static string ResolveDisplayNameEscaped(AppUser? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.FullName))
            return user.FullName;

        if (!string.IsNullOrWhiteSpace(user?.Username))
            return user.Username;

        return "Kh\u00E1ch VKFood";
    }

    private static string GetRoleDisplayEscaped(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "admin" => "Qu\u1EA3n tr\u1ECB vi\u00EAn",
            "user" => "Ng\u01B0\u1EDDi d\u00F9ng",
            _ => "Kh\u00E1ch"
        };
    }

    private static string GetLanguageDisplayEscaped(string languageCode)
    {
        return languageCode switch
        {
            "en" => "English",
            "zh" => "\u4E2D\u6587",
            "ja" => "\u65E5\u672C\u8A9E",
            "de" => "Deutsch",
            _ => "Ti\u1EBFng Vi\u1EC7t"
        };
    }

    private static string GetUserTypeDisplayEscaped(string? userType)
    {
        return userType?.Trim().ToLowerInvariant() switch
        {
            "tourist" => "Kh\u00E1ch du l\u1ECBch",
            _ => "Kh\u00E1ch n\u1ED9i \u0111\u1ECBa"
        };
    }

    private static string GetModeDisplayEscaped(string mode)
    {
        return mode switch
        {
            "Auto" => "T\u1EF1 \u0111\u1ED9ng",
            "Audio" => "Audio",
            _ => "TTS"
        };
    }

    private void RefreshUserInfoClean()
    {
        var user = _authService.CurrentUser;
        var fullName = ResolveDisplayNameClean(user);
        var currentLanguage = AppLanguageService.NormalizeLanguage(_languageService.CurrentLanguage);
        var currentMode = NormalizeNarrationMode(_settingsService.NarrationOutputMode);
        var isLoggedIn = user is not null;
        var isActive = user?.IsActive ?? false;

        AvatarLabel.Text = BuildInitials(fullName);
        FullNameLabel.Text = fullName;
        UsernameLabel.Text = user is null
            ? "Chưa có tài khoản đang đăng nhập"
            : $"@{user.Username}";

        RoleBadgeLabel.Text = GetRoleDisplayClean(user?.Role);
        StatusBadgeLabel.Text = !isLoggedIn
            ? "Khách"
            : isActive ? "Đang hoạt động" : "Tạm khóa";

        CurrentLanguageValueLabel.Text = GetLanguageDisplayClean(currentLanguage);
        CurrentLanguageHintLabel.Text = $"{GetUserTypeDisplayClean(_languageService.UserType)} đang dùng";
        UserTypeValueLabel.Text = GetUserTypeDisplayClean(_languageService.UserType);
        NarrationModeValueLabel.Text = $"Chế độ phát: {GetModeDisplayClean(currentMode)}";

        UsernameValueLabel.Text = user?.Username ?? "--";
        RoleValueLabel.Text = GetRoleDisplayClean(user?.Role);
        StatusValueLabel.Text = !isLoggedIn
            ? "Chưa đăng nhập"
            : isActive ? "Sẵn sàng sử dụng" : "Tài khoản đang bị khóa";
        FooterNoteLabel.Text = "Đăng xuất sẽ đưa bạn về màn hình đăng nhập và chọn lại ngôn ngữ khi vào app.";
    }

    private async void OnLogoutClickedClean(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Đăng xuất",
            "Bạn có muốn đăng xuất khỏi tài khoản hiện tại không?",
            "Đăng xuất",
            "Hủy");

        if (!confirmed)
            return;

        _authService.Logout();

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<LoginPage>());
    }

    private static string ResolveDisplayName(AppUser? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.FullName))
            return user.FullName;

        if (!string.IsNullOrWhiteSpace(user?.Username))
            return user.Username;

        return "Khách VKFood";
    }

    private static string GetRoleDisplay(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "admin" => "Quản trị viên",
            "user" => "Người dùng",
            _ => "Khách"
        };
    }

    private static string GetLanguageDisplay(string languageCode)
    {
        return languageCode switch
        {
            "en" => "English",
            "zh" => "中文",
            "ja" => "日本語",
            "de" => "Deutsch",
            _ => "Tiếng Việt"
        };
    }

    private static string GetUserTypeDisplay(string? userType)
    {
        return userType?.Trim().ToLowerInvariant() switch
        {
            "tourist" => "Khách du lịch",
            _ => "Khách nội địa"
        };
    }

    private static string NormalizeNarrationMode(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode)
            ? "TTS"
            : mode.Trim();
    }

    private static string GetModeDisplay(string mode)
    {
        return mode switch
        {
            "Auto" => "Tự động",
            "Audio" => "Audio",
            _ => "TTS"
        };
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

    private static string ResolveDisplayNameClean(AppUser? user)
    {
        if (!string.IsNullOrWhiteSpace(user?.FullName))
            return user.FullName;

        if (!string.IsNullOrWhiteSpace(user?.Username))
            return user.Username;

        return "Khách VKFood";
    }

    private static string GetRoleDisplayClean(string? role)
    {
        return role?.Trim().ToLowerInvariant() switch
        {
            "admin" => "Quản trị viên",
            "user" => "Người dùng",
            _ => "Khách"
        };
    }

    private static string GetLanguageDisplayClean(string languageCode)
    {
        return languageCode switch
        {
            "en" => "English",
            "zh" => "中文",
            "ja" => "日本語",
            "de" => "Deutsch",
            _ => "Tiếng Việt"
        };
    }

    private static string GetUserTypeDisplayClean(string? userType)
    {
        return userType?.Trim().ToLowerInvariant() switch
        {
            "tourist" => "Khách du lịch",
            _ => "Khách nội địa"
        };
    }

    private static string GetModeDisplayClean(string mode)
    {
        return mode switch
        {
            "Auto" => "Tự động",
            "Audio" => "Audio",
            _ => "TTS"
        };
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
        NavHomeButton.Text = $"🏠\n{_text["Nav.Home"]}";
        NavMapButton.Text = $"🗺\n{_text["Nav.Map"]}";
        NavHistoryButton.Text = $"🕘\n{_text["Nav.History"]}";
        NavAccountButton.Text = $"👤\n{_text["Nav.Account"]}";
    }

    private void ApplyLocalizedTextClean()
    {
        ApplyLocalizedText();
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }
}
