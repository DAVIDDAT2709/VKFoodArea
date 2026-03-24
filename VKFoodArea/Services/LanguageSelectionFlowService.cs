namespace VKFoodArea.Services;

public class LanguageSelectionFlowService
{
    private readonly AppLanguageService _languageService;
    private readonly AppSettingsService _settingsService;

    public LanguageSelectionFlowService(
        AppLanguageService languageService,
        AppSettingsService settingsService)
    {
        _languageService = languageService;
        _settingsService = settingsService;
    }

    public async Task ShowLanguageSelectionAsync(Page page)
    {
        var visitorChoice = await page.DisplayActionSheet(
            "Chọn loại người dùng",
            "Hủy",
            null,
            "Khách nội địa",
            "Tourists");

        switch (visitorChoice)
        {
            case "Khách nội địa":
                ApplyDomestic();
                return;

            case "Tourists":
                await ShowTouristLanguageSelectionAsync(page);
                return;

            default:
                SyncSettingsWithCurrentLanguage();
                return;
        }
    }

    private async Task ShowTouristLanguageSelectionAsync(Page page)
    {
        var languageChoice = await page.DisplayActionSheet(
            "Choose language",
            "Cancel",
            null,
            "English",
            "中文",
            "日本語",
            "Deutsch");

        switch (languageChoice)
        {
            case "English":
                ApplyTourist("en");
                break;

            case "中文":
                ApplyTourist("zh");
                break;

            case "日本語":
                ApplyTourist("ja");
                break;

            case "Deutsch":
                ApplyTourist("de");
                break;

            default:
                SyncSettingsWithCurrentLanguage();
                break;
        }
    }

    private void ApplyDomestic()
    {
        _languageService.SetDomestic();
        _settingsService.NarrationLanguage = "vi";
    }

    private void ApplyTourist(string language)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        _languageService.SetTourist(normalizedLanguage);
        _settingsService.NarrationLanguage = normalizedLanguage;
    }

    private void SyncSettingsWithCurrentLanguage()
    {
        _settingsService.NarrationLanguage = _languageService.CurrentLanguage;
    }
}