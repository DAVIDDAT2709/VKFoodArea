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
    private readonly AppTextService _text;

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
        NarrationService narrationService,
        AppTextService text)
    {
        InitializeComponent();
        _settings = settings;
        _languageService = languageService;
        _narrationService = narrationService;
        _text = text;

        LanguagePicker.ItemsSource = _languages;
        LanguagePicker.ItemDisplayBinding = new Binding(nameof(LanguageItem.DisplayName));

        ModePicker.ItemsSource = _modes;

        LanguagePicker.SelectedIndexChanged += OnSelectionChanged;
        ModePicker.SelectedIndexChanged += OnSelectionChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();

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
        _text.SetLanguage(languageCode);

        ApplyLocalizedText();
        SummaryLabel.Text = _text.Format("Settings.SaveSummary", languageName, _text.GetModeDisplay(selectedMode));
        await DisplayAlert(_text["Settings.SaveAlertTitle"], _text["Settings.SaveAlertMessage"], _text["Common.Ok"]);
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        try
        {
            var selectedLanguage = LanguagePicker.SelectedItem as LanguageItem ?? _languages[0];
            var languageCode = selectedLanguage.Code;

            await _narrationService.PreviewAsync(_text.GetPreviewText(languageCode), languageCode);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                _text["Settings.PreviewErrorTitle"],
                _text.Format("Settings.PreviewErrorMessage", ex.Message),
                _text["Common.Ok"]);
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
            ? _text.Format("Settings.SaveSummary", selectedLanguage.DisplayName, _text.GetModeDisplay(selectedMode))
            : _text.Format("Settings.CurrentSummary", selectedLanguage.DisplayName, _text.GetModeDisplay(selectedMode));
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Settings.PageTitle"];
        HeaderTitleLabel.Text = _text["Settings.HeaderTitle"];
        LanguageSectionLabel.Text = _text["Settings.LanguageSection"];
        ModeSectionLabel.Text = _text["Settings.ModeSection"];
        PreviewTitleLabel.Text = _text["Settings.PreviewTitle"];
        PreviewMetaLabel.Text = _text["PoiDetail.AudioGuide"];
        PreviewButton.Text = _text["Settings.PreviewButton"];
        SaveButton.Text = _text["Common.Save"];
        LanguagePicker.Title = _text["Settings.LanguagePickerTitle"];
        ModePicker.Title = _text["Settings.ModePickerTitle"];
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
