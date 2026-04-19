using System.Linq;
using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Startup;

public partial class HomeEntryPage : ContentPage
{
    private readonly LanguageSelectionFlowService _languageSelectionFlowService;
    private readonly AppSettingsService _settingsService;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly AppLinkService _appLinkService;
    private readonly AppTextService _text;
    private readonly AppLanguageService _languageService;

    private string _selectedUserType = "domestic";
    private string _selectedLanguage = "vi";

    public HomeEntryPage(
        LanguageSelectionFlowService languageSelectionFlowService,
        AppSettingsService settingsService,
        AppRootNavigationService rootNavigationService,
        AppLinkService appLinkService,
        AppTextService text,
        AppLanguageService languageService)
    {
        InitializeComponent();
        _languageSelectionFlowService = languageSelectionFlowService;
        _settingsService = settingsService;
        _rootNavigationService = rootNavigationService;
        _appLinkService = appLinkService;
        _text = text;
        _languageService = languageService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        SyncSelectionFromPreferences();
        ApplyLocalizedText();
        BuildLanguageButtons();
        ApplySelectionVisuals();
    }

    private void SyncSelectionFromPreferences()
    {
        var currentLanguage = AppLanguageService.NormalizeLanguage(_languageService.CurrentLanguage);
        var isTourist = string.Equals(_languageService.UserType, "tourist", StringComparison.OrdinalIgnoreCase);

        _selectedUserType = isTourist ? "tourist" : "domestic";
        _selectedLanguage = isTourist
            ? currentLanguage == "vi" ? "en" : currentLanguage
            : "vi";
    }

    private async void OnEnterClicked(object sender, EventArgs e)
    {
        if (_selectedUserType == "domestic")
            _languageSelectionFlowService.ApplyDomestic();
        else
            _languageSelectionFlowService.ApplyTourist(_selectedLanguage);

        _settingsService.HasCompletedEntryFlow = true;
        await _rootNavigationService.SetRootAsync<HomeDesignPage>();
        await _appLinkService.TryHandlePendingAsync();
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

    private void OnLanguageButtonClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not string language)
            return;

        _selectedLanguage = language;
        ApplySelectionVisuals();
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Nav.Home"];
        HeroSubtitleLabel.Text = GetHeroSubtitleText();
        SelectionTitleLabel.Text = _text["Login.SelectionTitle"];
        SelectionSubtitleLabel.Text = GetSelectionSubtitleText();
        DomesticTitleLabel.Text = _text["Login.DomesticTitle"];
        DomesticDescriptionLabel.Text = _text["Login.DomesticDescription"];
        TouristTitleLabel.Text = _text["Login.TouristTitle"];
        TouristDescriptionLabel.Text = _text["Login.TouristDescription"];
        LanguageTitleLabel.Text = _text["Login.LanguageTitle"];
        EnterButton.Text = _text["Login.EnterButton"];
        FooterNoteLabel.Text = GetFooterNoteText();
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
                CornerRadius = 8,
                FontAttributes = FontAttributes.Bold,
                FontSize = 13,
                HeightRequest = 42,
                MinimumWidthRequest = 108,
                Padding = new Thickness(14, 8),
                Margin = new Thickness(0, 0, 10, 10)
            };

            button.Clicked += OnLanguageButtonClicked;
            LanguageOptionsLayout.Children.Add(button);
        }

        NormalizeLanguageButtonText();
    }

    private void NormalizeLanguageButtonText()
    {
        foreach (var button in LanguageOptionsLayout.Children.OfType<Button>())
        {
            button.Text = button.CommandParameter?.ToString() switch
            {
                "zh" => "\u4e2d\u6587",
                "ja" => "\u65e5\u672c\u8a9e",
                _ => button.Text
            };
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

    private string GetHeroSubtitleText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "Choose the way you want the app to speak and guide you.",
            "zh" => "选择应用要如何陪你使用与讲解。",
            "ja" => "アプリの案内と言語の使い方を選びます。",
            "de" => "Wählen Sie, wie die App Sie sprachlich begleiten soll.",
            _ => "Chọn cách app sẽ đồng hành và thuyết minh cho bạn."
        };
    }

    private string GetSelectionSubtitleText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "You can change this later in Account. It only tunes language and narration behavior.",
            "zh" => "之后可以在“账户”里再改。这里仅影响语言与讲解方式。",
            "ja" => "あとからアカウントで変更できます。ここでは言語と音声案内だけを調整します。",
            "de" => "Sie können dies später im Konto ändern. Hier werden nur Sprache und Audioführung festgelegt.",
            _ => "Bạn có thể đổi lại sau trong Tài khoản. Mục này chỉ chỉnh ngôn ngữ và cách phát thuyết minh."
        };
    }

    private string GetFooterNoteText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "Changing this later does not remove your saved data or account.",
            "zh" => "之后再修改也不会删除你的数据或账户。",
            "ja" => "あとで変更しても、保存済みデータやアカウントは消えません。",
            "de" => "Eine spätere Änderung löscht weder gespeicherte Daten noch Ihr Konto.",
            _ => "Đổi lại sau cũng không làm mất dữ liệu hay tài khoản đang có."
        };
    }
}
