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

    public UserPage(
        AuthService authService,
        AppLanguageService languageService,
        AppSettingsService settingsService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _authService = authService;
        _languageService = languageService;
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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
}
