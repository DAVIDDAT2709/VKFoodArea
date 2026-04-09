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
            await DisplayAlert(_text["Common.Error"], result.Message, _text["Common.Ok"]);
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

        await DisplayAlert(title, message, _text["Common.Ok"]);
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
            await DisplayAlert(
                _text["Settings.PreviewErrorTitle"],
                _text.Format("Settings.PreviewErrorMessage", ex.Message),
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
        HeaderSubtitleLabel.Text = _text["Settings.HeaderTitle"];
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
}
