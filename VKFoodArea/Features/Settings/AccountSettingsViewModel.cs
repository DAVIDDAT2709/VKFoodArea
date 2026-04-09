using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Settings;

public class AccountSettingsViewModel : INotifyPropertyChanged
{
    private readonly AccountService _accountService;
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private string _username = string.Empty;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private LanguageOption? _selectedLanguage;
    private string _selectedOutputMode = "TTS";
    private string _summaryText = string.Empty;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } =
    [
        new("Tiếng Việt", "vi"),
        new("English", "en"),
        new("中文", "zh"),
        new("日本語", "ja"),
        new("Deutsch", "de")
    ];

    public ObservableCollection<string> OutputModeOptions { get; } =
    [
        "Auto",
        "Audio",
        "TTS"
    ];

    public AccountSettingsViewModel(
        AccountService accountService,
        NarrationService narrationService,
        AppTextService text)
    {
        _accountService = accountService;
        _narrationService = narrationService;
        _text = text;
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
        }
    }

    public string FullName
    {
        get => _fullName;
        set
        {
            _fullName = value;
            OnPropertyChanged();
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            _email = value;
            OnPropertyChanged();
        }
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
            _selectedOutputMode = value;
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

    public async Task<AccountSettingsViewResult> LoadAccountSettingsAsync()
    {
        var snapshot = await _accountService.GetUserProfileAsync();
        if (snapshot is null)
            return AccountSettingsViewResult.Fail(_text["User.NoAccount"]);

        ApplySnapshot(snapshot);
        return AccountSettingsViewResult.Success();
    }

    public async Task<AccountSettingsViewResult> UpdateProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
            return AccountSettingsViewResult.Fail(_text["Register.RequiredError"]);

        if (!LooksLikeEmail(Email))
            return AccountSettingsViewResult.Fail(_text["Register.InvalidEmailError"]);

        var result = await _accountService.UpdateUserProfileAsync(new AccountProfileUpdateRequest(
            FullName.Trim(),
            Email.Trim(),
            SelectedLanguage?.Code ?? "vi",
            SelectedOutputMode));

        if (!result.IsSuccess || result.Snapshot is null)
        {
            return AccountSettingsViewResult.Fail(result.ErrorCode switch
            {
                "duplicate_email" => _text["Register.DuplicateEmailError"],
                "not_found" => _text["User.NoAccount"],
                "invalid_email" => _text["Register.InvalidEmailError"],
                _ => _text["Register.RequiredError"]
            });
        }

        ApplySnapshot(result.Snapshot);
        return AccountSettingsViewResult.Success(_text["Settings.SaveAlertMessage"]);
    }

    public Task PreviewAsync()
    {
        var languageCode = SelectedLanguage?.Code ?? "vi";
        return _narrationService.PreviewAsync(_text.GetPreviewText(languageCode), languageCode);
    }

    private void ApplySnapshot(AccountSettingsSnapshot snapshot)
    {
        Username = snapshot.Username;
        FullName = snapshot.FullName;
        Email = snapshot.Email;
        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == snapshot.Language) ?? LanguageOptions[0];
        SelectedOutputMode = OutputModeOptions.FirstOrDefault(x => x == snapshot.PlaybackMode) ?? "TTS";
        UpdateSummaryText();
    }

    private void UpdateSummaryText()
    {
        var language = SelectedLanguage?.Code ?? "vi";
        SummaryText = _text.Format(
            "Settings.CurrentSummary",
            _text.GetLanguageDisplay(language),
            _text.GetModeDisplay(SelectedOutputMode));
    }

    private static bool LooksLikeEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email.Trim());
            return address.Address == email.Trim();
        }
        catch
        {
            return false;
        }
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

public sealed record AccountSettingsViewResult(bool IsSuccess, string? Message = null)
{
    public static AccountSettingsViewResult Success(string? message = null) => new(true, message);

    public static AccountSettingsViewResult Fail(string message) => new(false, message);
}
