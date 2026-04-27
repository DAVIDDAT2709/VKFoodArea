using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Home;
using VKFoodArea.Features.Settings;
using VKFoodArea.Features.Startup;
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
        var isActive = user?.IsActive ?? true;

        AvatarLabel.Text = BuildInitials(fullName);
        FullNameLabel.Text = fullName;
        UsernameLabel.Text = user is null
            ? _text["User.Guest"]
            : $"@{user.Username}";

        RoleBadgeLabel.Text = GetRoleDisplayLocalized(user?.Role);
        StatusBadgeLabel.Text = !isLoggedIn
            ? _text["User.Ready"]
            : isActive ? _text["User.Active"] : _text["User.Disabled"];

        CurrentLanguageValueLabel.Text = _text.GetLanguageDisplay(currentLanguage);
        CurrentLanguageHintLabel.Text = BuildLanguageHint(isLoggedIn);
        UserTypeValueLabel.Text = _text.GetUserTypeDisplay(_languageService.UserType);
        NarrationModeValueLabel.Text = _text.GetModeDisplay(currentMode);

        UsernameValueLabel.Text = user?.Username ?? _text["User.Guest"];
        RoleValueLabel.Text = GetRoleDisplayLocalized(user?.Role);
        StatusValueLabel.Text = !isLoggedIn
            ? _text["User.Ready"]
            : isActive ? _text["User.Ready"] : _text["User.Locked"];
        FooterNoteLabel.Text = BuildFooterNote(isLoggedIn);
    }

    private async void OnOpenSettingsClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(page);
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

    private async void OnChooseLanguageClicked(object sender, EventArgs e)
    {
        await _rootNavigationService.SetRootAsync<HomeEntryPage>();
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

        return "Khach VKFood";
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
        var normalizedRole = AppUserRoleNames.Normalize(role);

        return _text.CurrentLanguage switch
        {
            "en" => normalizedRole switch
            {
                AppUserRoleNames.Admin => "Administrator",
                AppUserRoleNames.Operator => "Operator",
                AppUserRoleNames.User => "User",
                _ => _text["User.Guest"]
            },
            "zh" => normalizedRole switch
            {
                AppUserRoleNames.Admin => "管理员",
                AppUserRoleNames.Operator => "运营人员",
                AppUserRoleNames.User => "用户",
                _ => _text["User.Guest"]
            },
            "ja" => normalizedRole switch
            {
                AppUserRoleNames.Admin => "管理者",
                AppUserRoleNames.Operator => "運用担当",
                AppUserRoleNames.User => "利用者",
                _ => _text["User.Guest"]
            },
            "de" => normalizedRole switch
            {
                AppUserRoleNames.Admin => "Administrator",
                AppUserRoleNames.Operator => "Operator",
                AppUserRoleNames.User => "Benutzer",
                _ => _text["User.Guest"]
            },
            _ => normalizedRole switch
            {
                AppUserRoleNames.Admin => "Quản trị viên",
                AppUserRoleNames.Operator => "Điều phối nội bộ",
                AppUserRoleNames.User => "Người dùng",
                _ => _text["User.Guest"]
            }
        };
    }

    private string BuildLanguageHint(bool isLoggedIn)
    {
        return _text.CurrentLanguage switch
        {
            "en" => isLoggedIn
                ? "This account keeps your current narration preferences."
                : "Your current setup is stored on this device.",
            "zh" => isLoggedIn
                ? "当前账号会记住你的讲解设置。"
                : "当前设置会保存在这台设备上。",
            "ja" => isLoggedIn
                ? "現在のアカウントに音声案内設定が保存されます。"
                : "現在の設定はこの端末に保存されます。",
            "de" => isLoggedIn
                ? "Dieses Konto merkt sich Ihre aktuellen Audioeinstellungen."
                : "Die aktuellen Einstellungen werden auf diesem Geraet gespeichert.",
            _ => isLoggedIn
                ? "Tài khoản này sẽ nhớ ngôn ngữ và cách phát hiện tại."
                : "Thiết lập hiện tại đang được lưu trên thiết bị này."
        };
    }

    private string BuildFooterNote(bool isLoggedIn)
    {
        return _text.CurrentLanguage switch
        {
            "en" => isLoggedIn
                ? "You can change the app entry language later without signing out."
                : "You can change the app entry language later in this screen.",
            "zh" => isLoggedIn
                ? "之后可以再改进入应用时的语言，不会让你退出账号。"
                : "之后也可以在这里再改进入应用时的语言。",
            "ja" => isLoggedIn
                ? "あとからアプリ開始時の言語を変えても、ログアウトにはなりません。"
                : "アプリ開始時の言語はあとからここで変更できます。",
            "de" => isLoggedIn
                ? "Sie koennen die Einstiegssprache spaeter aendern, ohne sich abzumelden."
                : "Die Einstiegssprache kann spaeter hier geaendert werden.",
            _ => isLoggedIn
                ? "Bạn có thể đổi lại ngôn ngữ khi vào app mà không bị đăng xuất."
                : "Bạn có thể đổi lại cách vào app ngay tại màn hình này."
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
        ChooseLanguageButton.Text = _text.CurrentLanguage switch
        {
            "en" => "Change app language",
            "zh" => "调整进入应用的语言",
            "ja" => "開始時の言語を変更",
            "de" => "App-Sprache beim Start aendern",
            _ => "Đổi ngôn ngữ khi vào app"
        };
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }
}
