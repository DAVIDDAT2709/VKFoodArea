using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public partial class SettingsPage : ContentPage
{
    private readonly AccountSettingsViewModel _viewModel;
    private readonly AppTextService _text;

    public SettingsPage(
        AccountSettingsViewModel viewModel,
        AppTextService text)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _text = text;
        BindingContext = _viewModel;

        LanguagePicker.ItemsSource = _viewModel.LanguageOptions;
        LanguagePicker.ItemDisplayBinding = new Binding(nameof(AccountSettingsViewModel.LanguageOption.DisplayName));
        ModePicker.ItemsSource = _viewModel.OutputModeOptions;

        LanguagePicker.SelectedIndexChanged += OnSelectionChanged;
        ModePicker.SelectedIndexChanged += OnSelectionChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var result = await _viewModel.LoadAccountSettingsAsync();
        SyncControlsFromViewModel();
        ApplyLocalizedText();

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
            await DisplayAlert(_text["Common.Error"], result.Message, _text["Common.Ok"]);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SyncViewModelFromControls();
        var result = await _viewModel.UpdateProfileAsync();
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
            await _viewModel.PreviewAsync();
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
        Title = _text["User.PageTitle"];
        HeaderTitleLabel.Text = _text["User.PageTitle"];
        ProfileSectionLabel.Text = _text["User.AccountInfo"];
        UsernameLabel.Text = _text["User.Username"];
        FullNameLabel.Text = _text["Register.FullNameLabel"];
        EmailLabel.Text = _text["Register.EmailLabel"];
        FullNameEntry.Placeholder = _text["Register.FullNamePlaceholder"];
        EmailEntry.Placeholder = _text["Register.EmailPlaceholder"];
        LanguageSectionLabel.Text = _text["Settings.LanguageSection"];
        ModeSectionLabel.Text = _text["Settings.ModeSection"];
        PreviewTitleLabel.Text = _text["Settings.PreviewTitle"];
        PreviewMetaLabel.Text = _text["PoiDetail.AudioGuide"];
        PreviewButton.Text = _text["Settings.PreviewButton"];
        SaveButton.Text = _text["Common.Save"];
        LanguagePicker.Title = _text["Settings.LanguagePickerTitle"];
        ModePicker.Title = _text["Settings.ModePickerTitle"];
        SummaryLabel.Text = _viewModel.SummaryText;
    }

    private void SyncControlsFromViewModel()
    {
        UsernameValueLabel.Text = string.IsNullOrWhiteSpace(_viewModel.Username)
            ? "--"
            : _viewModel.Username;
        FullNameEntry.Text = _viewModel.FullName;
        EmailEntry.Text = _viewModel.Email;
        LanguagePicker.SelectedItem = _viewModel.SelectedLanguage;
        ModePicker.SelectedItem = _viewModel.SelectedOutputMode;
        SummaryLabel.Text = _viewModel.SummaryText;
    }

    private void SyncViewModelFromControls()
    {
        _viewModel.FullName = FullNameEntry.Text?.Trim() ?? string.Empty;
        _viewModel.Email = EmailEntry.Text?.Trim() ?? string.Empty;
        _viewModel.SelectedLanguage = LanguagePicker.SelectedItem as AccountSettingsViewModel.LanguageOption
                                      ?? _viewModel.LanguageOptions.First();
        _viewModel.SelectedOutputMode = ModePicker.SelectedItem?.ToString() ?? "TTS";
    }
}
