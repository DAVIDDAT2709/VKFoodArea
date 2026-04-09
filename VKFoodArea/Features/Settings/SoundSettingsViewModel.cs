using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public class SoundSettingsViewModel : INotifyPropertyChanged
{
    private readonly SoundSettingsService _soundSettingsService;
    private readonly TtsAudioPreviewService _previewService;
    private readonly AppTextService _text;
    private LanguageOption? _selectedLanguage;
    private string _selectedOutputMode = "TTS";
    private string _summaryText = string.Empty;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = [];

    public ObservableCollection<string> OutputModeOptions { get; } =
    [
        "Auto",
        "Audio",
        "TTS"
    ];

    public SoundSettingsViewModel(
        SoundSettingsService soundSettingsService,
        TtsAudioPreviewService previewService,
        AppTextService text)
    {
        _soundSettingsService = soundSettingsService;
        _previewService = previewService;
        _text = text;
        RefreshLanguageOptions();
        _selectedLanguage = LanguageOptions.FirstOrDefault();
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _selectedLanguage = value;
            OnPropertyChanged();
            UpdateSummaryText();
        }
    }

    public string SelectedOutputMode
    {
        get => _selectedOutputMode;
        set
        {
            _selectedOutputMode = SoundSettingsService.NormalizePlaybackMode(value);
            OnPropertyChanged();
            UpdateSummaryText();
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set
        {
            _summaryText = value;
            OnPropertyChanged();
        }
    }

    public async Task<SoundSettingsViewResult> LoadSoundSettingsAsync()
    {
        RefreshLanguageOptions();
        var snapshot = await _soundSettingsService.GetCurrentSoundSettingsAsync();
        ApplySnapshot(snapshot);
        return SoundSettingsViewResult.Success();
    }

    public async Task<SoundSettingsViewResult> SaveSoundSettingsAsync()
    {
        var result = await _soundSettingsService.UpdateSoundSettingsAsync(
            SelectedLanguage?.Code,
            SelectedOutputMode);

        if (!result.IsSuccess || result.Snapshot is null)
        {
            return SoundSettingsViewResult.Fail(result.ErrorCode switch
            {
                "not_found" => _text["User.NoAccount"],
                _ => _text["Common.Error"]
            });
        }

        ApplySnapshot(result.Snapshot);
        return SoundSettingsViewResult.Success(_text["Settings.SaveAlertMessage"]);
    }

    public Task PreviewSoundAsync()
    {
        return _previewService.PlayPreviewAsync(
            SelectedLanguage?.Code,
            SelectedOutputMode);
    }

    private void ApplySnapshot(SoundSettingsSnapshot snapshot)
    {
        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == snapshot.Language) ?? LanguageOptions.First();
        SelectedOutputMode = OutputModeOptions.FirstOrDefault(x => x == snapshot.PlaybackMode) ?? "TTS";
        UpdateSummaryText();
    }

    private void RefreshLanguageOptions()
    {
        var previousCode = SelectedLanguage?.Code;
        var items = new[]
        {
            new LanguageOption(_text.GetLanguageDisplay("vi"), "vi"),
            new LanguageOption(_text.GetLanguageDisplay("en"), "en"),
            new LanguageOption(_text.GetLanguageDisplay("zh"), "zh"),
            new LanguageOption(_text.GetLanguageDisplay("ja"), "ja"),
            new LanguageOption(_text.GetLanguageDisplay("de"), "de")
        };

        LanguageOptions.Clear();
        foreach (var item in items)
            LanguageOptions.Add(item);

        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == previousCode) ?? LanguageOptions.FirstOrDefault();
    }

    private void UpdateSummaryText()
    {
        var language = SelectedLanguage?.Code ?? "vi";
        SummaryText = _text.Format(
            "Settings.CurrentSummary",
            _text.GetLanguageDisplay(language),
            _text.GetModeDisplay(SelectedOutputMode));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed record LanguageOption(string DisplayName, string Code)
    {
        public override string ToString() => DisplayName;
    }
}

public sealed record SoundSettingsViewResult(bool IsSuccess, string? Message = null)
{
    public static SoundSettingsViewResult Success(string? message = null) => new(true, message);

    public static SoundSettingsViewResult Fail(string message) => new(false, message);
}
