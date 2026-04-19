using System;
using System.Linq;
using Microsoft.Maui.Controls;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly SoundSettingsViewModel _viewModel;
    private readonly AppTextService _text;

    public SettingsPage(
        SoundSettingsViewModel viewModel,
        AppTextService text)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _text = text;
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

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
            await DisplayAlertAsync(_text["Common.Error"], result.Message, _text["Common.Ok"]);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SyncViewModelFromControls();
        var result = await _viewModel.SaveSoundSettingsAsync();
        ApplyLocalizedText();
        SyncControlsFromViewModel();

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
