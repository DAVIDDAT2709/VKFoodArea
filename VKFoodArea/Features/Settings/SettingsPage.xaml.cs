using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Maui.Controls;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SoundSettingsViewModel _viewModel;
    private readonly AppTextService _text;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly IHttpClientFactory _httpClientFactory;

    public SettingsPage(
        SoundSettingsViewModel viewModel,
        AppTextService text,
        ApiBaseUrlService apiBaseUrlService,
        IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _text = text;
        _apiBaseUrlService = apiBaseUrlService;
        _httpClientFactory = httpClientFactory;
        BindingContext = _viewModel;

        LanguagePicker.ItemsSource = _viewModel.LanguageOptions;
        LanguagePicker.ItemDisplayBinding = new Binding(nameof(SoundSettingsViewModel.LanguageOption.DisplayName));
        ModePicker.ItemsSource = _viewModel.OutputModeOptions;

        LanguagePicker.SelectedIndexChanged += OnSelectionChanged;
        ModePicker.SelectedIndexChanged += OnSelectionChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var result = await _viewModel.LoadSoundSettingsAsync();
        SyncControlsFromViewModel();
        ApplyLocalizedText();
        ApplyConnectionStatus();

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
            await DisplayAlertAsync(_text["Common.Error"], result.Message, _text["Common.Ok"]);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SyncViewModelFromControls();
        var result = await _viewModel.SaveSoundSettingsAsync();
        ApplyLocalizedText();
        SyncControlsFromViewModel();
        ApplyConnectionStatus();

        var title = result.IsSuccess
            ? _text["Settings.SaveAlertTitle"]
            : _text["Common.Error"];
        var message = result.Message ?? _text["Common.Error"];

        await DisplayAlertAsync(title, message, _text["Common.Ok"]);
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        SyncViewModelFromControls();

        try
        {
            await _viewModel.PreviewSoundAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                _text["Settings.PreviewErrorTitle"],
                FriendlyErrorMessages.Get(ex, _text, FriendlyErrorContext.Preview),
                _text["Common.Ok"]);
        }
    }

    private async void OnTestConnectionClicked(object sender, EventArgs e)
    {
        if (!_apiBaseUrlService.TryBuildApiUrl("api/pois", out var url))
        {
            ApplyConnectionStatus("Chưa có endpoint web. Hãy quét QR từ website hoặc nhập URL demo.", false);
            return;
        }

        TestConnectionButton.IsEnabled = false;
        ApplyConnectionStatus("Đang kiểm tra kết nối tới web...", null);

        try
        {
            using var response = await _httpClientFactory.CreateClient("DemoHttp").GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                ApplyConnectionStatus("Kết nối OK. App đọc được API POI từ web.", true);
            }
            else
            {
                ApplyConnectionStatus($"Web phản hồi {(int)response.StatusCode}. Kiểm tra lại URL hoặc server.", false);
            }
        }
        catch (Exception ex)
        {
            ApplyConnectionStatus(FriendlyErrorMessages.Get(ex, _text, FriendlyErrorContext.Startup), false);
        }
        finally
        {
            TestConnectionButton.IsEnabled = _apiBaseUrlService.HasConfiguredBaseUrl;
        }
    }

    private async void OnEditEndpointClicked(object sender, EventArgs e)
    {
        if (!_apiBaseUrlService.CanUseDemoTools)
            return;

        var value = await DisplayPromptAsync(
            "Web endpoint",
            "Nhập URL web đang chạy. Để trống để app quay lại URL chính thức hoặc URL tự nhận từ QR.",
            "Lưu",
            _text["Common.Cancel"],
            initialValue: _apiBaseUrlService.BaseUrl,
            keyboard: Keyboard.Url);

        if (value is null)
            return;

        var result = _apiBaseUrlService.SaveDemoBaseUrl(value);
        ApplyConnectionStatus(result.Message, result.Success);

        if (!result.Success)
            await DisplayAlertAsync(_text["Common.Error"], result.Message, _text["Common.Ok"]);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        SyncViewModelFromControls();
        SummaryLabel.Text = _viewModel.SummaryText;
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Settings.PageTitle"];
        HeaderTitleLabel.Text = _text["Settings.PageTitle"];
        HeaderSubtitleLabel.Text = GetHeaderSubtitleText();
        LanguageSectionLabel.Text = _text["Settings.LanguageSection"];
        ModeSectionLabel.Text = _text["Settings.ModeSection"];
        PreviewTitleLabel.Text = _text["Settings.PreviewTitle"];
        PreviewMetaLabel.Text = _text["Settings.HeaderTitle"];
        PreviewButton.Text = _text["Settings.PreviewButton"];
        SaveButton.Text = _text["Common.Save"];
        LanguagePicker.Title = _text["Settings.LanguagePickerTitle"];
        ModePicker.Title = _text["Settings.ModePickerTitle"];
        SummaryLabel.Text = _viewModel.SummaryText;
        ConnectionSectionLabel.Text = "Kết nối web";
        TestConnectionButton.Text = "Kiểm tra";
        EditEndpointButton.Text = "Đổi URL";
    }

    private void ApplyConnectionStatus(string? overrideMessage = null, bool? isHealthy = null)
    {
        var hasEndpoint = _apiBaseUrlService.HasConfiguredBaseUrl;
        var sourceLabel = GetConnectionSourceLabel();

        ConnectionBadgeLabel.Text = isHealthy switch
        {
            true => "OK",
            false => "Cần kiểm tra",
            _ => hasEndpoint ? sourceLabel : "Chưa có URL"
        };

        ConnectionBadgeLabel.TextColor = isHealthy switch
        {
            true => Color.FromArgb("#129488"),
            false => Color.FromArgb("#B8452E"),
            _ => hasEndpoint ? Color.FromArgb("#129488") : Color.FromArgb("#8A5A00")
        };

        ConnectionStatusLabel.Text = overrideMessage ?? GetConnectionStatusText();
        ConnectionUrlLabel.Text = hasEndpoint ? _apiBaseUrlService.BaseUrl : "Chưa có endpoint web.";
        TestConnectionButton.IsEnabled = hasEndpoint;
        EditEndpointButton.IsVisible = _apiBaseUrlService.CanUseDemoTools;
        Grid.SetColumnSpan(TestConnectionButton, _apiBaseUrlService.CanUseDemoTools ? 1 : 2);
    }

    private string GetConnectionSourceLabel()
    {
        if (_apiBaseUrlService.IsUsingManualDemoBaseUrl)
            return "Demo";

        if (_apiBaseUrlService.IsUsingOfficialReleaseBaseUrl)
            return "Release";

        if (_apiBaseUrlService.IsUsingAutoDetectedBaseUrl)
            return "Từ QR";

        return "Tự động";
    }

    private string GetConnectionStatusText()
    {
        if (!_apiBaseUrlService.HasConfiguredBaseUrl)
            return "App chưa biết web endpoint. Quét QR từ website để tự nhận URL, hoặc nhập URL trong chế độ demo.";

        if (_apiBaseUrlService.IsUsingManualDemoBaseUrl)
            return "App đang dùng URL nhập tay cho demo. Dùng nút kiểm tra trước khi quét QR hoặc tải dữ liệu.";

        if (_apiBaseUrlService.IsUsingAutoDetectedBaseUrl)
            return "App đang dùng URL tự nhận từ QR gần nhất. Nếu đổi tunnel, hãy quét QR mới hoặc nhập URL mới.";

        if (_apiBaseUrlService.IsUsingOfficialReleaseBaseUrl)
            return "App đang dùng endpoint release được nhúng khi build.";

        return "App đã có endpoint web. Dùng nút kiểm tra để xác nhận API còn hoạt động.";
    }

    private void SyncControlsFromViewModel()
    {
        LanguagePicker.SelectedItem = _viewModel.SelectedLanguage;
        ModePicker.SelectedItem = _viewModel.SelectedOutputMode;
        SummaryLabel.Text = _viewModel.SummaryText;
    }

    private void SyncViewModelFromControls()
    {
        _viewModel.SelectedLanguage = LanguagePicker.SelectedItem as SoundSettingsViewModel.LanguageOption
                                      ?? _viewModel.LanguageOptions.First();
        _viewModel.SelectedOutputMode = ModePicker.SelectedItem?.ToString() ?? "TTS";
    }

    private string GetHeaderSubtitleText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "This page changes narration language and playback mode only. App interface language is changed from the app entry flow.",
            "zh" => "这里仅调整讲解语言与播放方式。界面语言请在进入应用时修改。",
            "ja" => "ここでは音声ガイドの言語と再生方法だけを変更します。画面言語はアプリ開始時の設定から変更できます。",
            "de" => "Hier ändern Sie nur Sprache und Wiedergabe der Audioführung. Die App-Sprache wird beim Einstieg angepasst.",
            _ => "Trang này chỉ đổi ngôn ngữ thuyết minh và chế độ phát. Ngôn ngữ giao diện được đổi ở bước vào app."
        };
    }
}
