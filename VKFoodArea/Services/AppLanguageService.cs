namespace VKFoodArea.Services;

public class AppLanguageService
{
    private const string LanguageKey = "app_language";
    private const string UserTypeKey = "user_type";

    public string CurrentLanguage
    {
        get => Preferences.Default.Get(LanguageKey, "vi");
        set => Preferences.Default.Set(LanguageKey, NormalizeLanguage(value));
    }

    public string UserType
    {
        get => Preferences.Default.Get(UserTypeKey, "domestic");
        set => Preferences.Default.Set(UserTypeKey, value);
    }

    public void SetDomestic()
    {
        UserType = "domestic";
        CurrentLanguage = "vi";
    }

    public void SetTourist(string language)
    {
        UserType = "tourist";
        CurrentLanguage = NormalizeLanguage(language);
    }

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "vi";

        return language.Trim().ToLowerInvariant() switch
        {
            "vi" or "vi-vn" => "vi",
            "en" or "en-us" or "en-gb" => "en",
            "zh" or "zh-cn" or "zh-tw" => "zh",
            "ja" or "ja-jp" => "ja",
            "de" or "de-de" => "de",
            _ => "vi"
        };
    }
}