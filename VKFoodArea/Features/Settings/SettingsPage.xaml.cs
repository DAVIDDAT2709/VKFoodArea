using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly AppSettingsService _settings;
    private readonly AppLanguageService _languageService;
    private readonly NarrationService _narrationService;

    private readonly List<LanguageItem> _languages = new()
    {
        new LanguageItem("Tiếng Việt", "vi"),
        new LanguageItem("English", "en"),
        new LanguageItem("中文", "zh"),
        new LanguageItem("日本語", "ja"),
        new LanguageItem("Deutsch", "de")
    };

    private readonly List<string> _modes = new()
    {
        "Auto",
        "Audio",
        "TTS"
    };

    public SettingsPage(
        AppSettingsService settings,
        AppLanguageService languageService,
        NarrationService narrationService)
    {
        InitializeComponent();
        _settings = settings;
        _languageService = languageService;
        _narrationService = narrationService;

        LanguagePicker.ItemsSource = _languages;
        LanguagePicker.ItemDisplayBinding = new Binding(nameof(LanguageItem.DisplayName));

        ModePicker.ItemsSource = _modes;

        LanguagePicker.SelectedIndexChanged += OnSelectionChanged;
        ModePicker.SelectedIndexChanged += OnSelectionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var lang = string.IsNullOrWhiteSpace(_settings.NarrationLanguage)
            ? "vi"
            : _settings.NarrationLanguage;

        var mode = string.IsNullOrWhiteSpace(_settings.NarrationOutputMode)
            ? "TTS"
            : _settings.NarrationOutputMode;

        LanguagePicker.SelectedItem = _languages.FirstOrDefault(x => x.Code == lang) ?? _languages[0];
        ModePicker.SelectedItem = _modes.Contains(mode) ? mode : "TTS";

        UpdateSummary(false);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var selectedLanguage = LanguagePicker.SelectedItem as LanguageItem ?? _languages[0];
        var selectedMode = ModePicker.SelectedItem?.ToString() ?? "TTS";

        var languageCode = selectedLanguage.Code;
        var languageName = selectedLanguage.DisplayName;

        _settings.NarrationLanguage = languageCode;
        _settings.NarrationOutputMode = selectedMode;

        _languageService.CurrentLanguage = languageCode;

        SummaryLabel.Text = $"Đã lưu: {languageName} | {selectedMode}";
        await DisplayAlert("Thành công", "Đã lưu cài đặt âm thanh.", "OK");
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        try
        {
            var selectedLanguage = LanguagePicker.SelectedItem as LanguageItem ?? _languages[0];
            var languageCode = selectedLanguage.Code;

            string previewText = languageCode switch
            {
                "en" => "Hello, this is a preview of the selected voice.",
                "zh" => "你好，这是所选语音的试听。",
                "ja" => "こんにちは。これは選択した音声の試聴です。",
                "de" => "Hallo, dies ist eine Vorschau der ausgewählten Stimme.",
                _ => "Xin chào, đây là phần nghe thử giọng đọc đã chọn."
            };

            await _narrationService.PreviewAsync(previewText, languageCode);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể nghe thử: {ex.Message}", "OK");
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        UpdateSummary(false);
    }

    private void UpdateSummary(bool saved)
    {
        var selectedLanguage = LanguagePicker.SelectedItem as LanguageItem ?? _languages[0];
        var selectedMode = ModePicker.SelectedItem?.ToString() ?? "TTS";

        SummaryLabel.Text = saved
            ? $"Đã lưu: {selectedLanguage.DisplayName} | {selectedMode}"
            : $"Hiện tại: {selectedLanguage.DisplayName} | {selectedMode}";
    }

    private sealed class LanguageItem
    {
        public LanguageItem(string displayName, string code)
        {
            DisplayName = displayName;
            Code = code;
        }

        public string DisplayName { get; }
        public string Code { get; }
    }
}
