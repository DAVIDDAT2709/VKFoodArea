namespace VKFoodArea.Services;

using Microsoft.Maui.Storage;

public class AppSettingsService
{
    private const string LanguageKey = "narration_language";
    private const string OutputModeKey = "narration_output_mode";

    public string NarrationLanguage
    {
        get => Preferences.Default.Get(LanguageKey, "vi");
        set => Preferences.Default.Set(LanguageKey, AppLanguageService.NormalizeLanguage(value));
    }

    public string NarrationOutputMode
    {
        get => Preferences.Default.Get(OutputModeKey, "TTS");
        set => Preferences.Default.Set(OutputModeKey, value);
    }
}