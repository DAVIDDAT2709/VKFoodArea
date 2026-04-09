namespace VKFoodArea.Services;

public class LanguageSelectionFlowService
{
    private readonly AppLanguageService _languageService;
    private readonly AppSettingsService _settingsService;
    private readonly AppTextService _textService;

    public LanguageSelectionFlowService(
        AppLanguageService languageService,
        AppSettingsService settingsService,
        AppTextService textService)
    {
        _languageService = languageService;
        _settingsService = settingsService;
        _textService = textService;
    }

    public void ApplyDomestic()
    {
        _languageService.SetDomestic();
        _settingsService.NarrationLanguage = "vi";
        _textService.SetLanguage("vi");
    }

    public void ApplyTourist(string language)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        _languageService.SetTourist(normalizedLanguage);
        _settingsService.NarrationLanguage = normalizedLanguage;
        _textService.SetLanguage(normalizedLanguage);
    }

    public void SyncSettingsWithCurrentLanguage()
    {
        _settingsService.NarrationLanguage = _languageService.CurrentLanguage;
        _textService.SetLanguage(_languageService.CurrentLanguage);
    }
}
