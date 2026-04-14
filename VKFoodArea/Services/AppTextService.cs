using System.Globalization;
using System.Text;

namespace VKFoodArea.Services;

public sealed partial class AppTextService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LanguageMaps =
        BuildLanguageMaps();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly AppLanguageService _languageService;

    public AppTextService(AppLanguageService languageService)
    {
        _languageService = languageService;
    }

    public string CurrentLanguage => AppLanguageService.NormalizeLanguage(_languageService.CurrentLanguage);

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var language = CurrentLanguage;

        if (LanguageMaps.TryGetValue(language, out var map) &&
            map.TryGetValue(key, out var localized))
        {
            return NormalizeStoredValue(localized);
        }

        return LanguageMaps["vi"].TryGetValue(key, out var fallback)
            ? NormalizeStoredValue(fallback)
            : key;
    }

    public string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public void SetLanguage(string language)
    {
        _languageService.CurrentLanguage = AppLanguageService.NormalizeLanguage(language);
    }

    public string GetLanguageDisplay(string? languageCode)
    {
        return NormalizeStoredValue(AppLanguageService.NormalizeLanguage(languageCode) switch
        {
            "en" => "English",
            "zh" => "中文",
            "ja" => "日本語",
            "de" => "Deutsch",
            _ => "Tiếng Việt"
        });
    }

    public string GetUserTypeDisplay(string? userType)
    {
        var isTourist = string.Equals(userType, "tourist", StringComparison.OrdinalIgnoreCase);

        return NormalizeStoredValue(CurrentLanguage switch
        {
            "en" => isTourist ? "Tourist" : "Local visitor",
            "zh" => isTourist ? "游客" : "本地游客",
            "ja" => isTourist ? "旅行者" : "ローカル利用者",
            "de" => isTourist ? "Tourist" : "Lokaler Besucher",
            _ => isTourist ? "Khách du lịch" : "Khách nội địa"
        });
    }

    public string GetModeDisplay(string? mode)
    {
        return NormalizeStoredValue((mode ?? "TTS").Trim() switch
        {
            "Auto" => CurrentLanguage switch
            {
                "en" => "Auto",
                "zh" => "自动",
                "ja" => "自動",
                "de" => "Automatik",
                _ => "Tự động"
            },
            "Audio" => "Audio",
            _ => "TTS"
        });
    }

    public string GetPreviewText(string? language)
    {
        return NormalizeStoredValue(AppLanguageService.NormalizeLanguage(language) switch
        {
            "en" => "Hello, this is a preview of the selected narration voice.",
            "zh" => "你好，这是当前语音讲解的试听内容。",
            "ja" => "こんにちは。これは選択した音声ガイドの試聴です。",
            "de" => "Hallo, das ist eine Vorschau der ausgewählten Sprecherstimme.",
            _ => "Xin chào, đây là phần nghe thử giọng đọc đã chọn."
        });
    }

    private static string NormalizeStoredValue(string value)
    {
        if (string.IsNullOrEmpty(value) || !(LooksLikeMojibake(value) || LooksLikeMojibakeFallback(value)))
            return value;

        try
        {
            if (!TryGetWindows1252Bytes(value, out var bytes))
                return value;

            var repaired = StrictUtf8.GetString(bytes);

            return repaired.Contains('\uFFFD')
                ? value
                : repaired;
        }
        catch (DecoderFallbackException)
        {
            return value;
        }
        catch
        {
            return value;
        }
    }

    private static bool LooksLikeMojibake(string value)
    {
        return value.Contains("Ã", StringComparison.Ordinal) ||
               value.Contains("Â", StringComparison.Ordinal) ||
               value.Contains("Ä", StringComparison.Ordinal) ||
               value.Contains("Å", StringComparison.Ordinal) ||
               value.Contains("Æ", StringComparison.Ordinal) ||
               value.Contains("á»", StringComparison.Ordinal) ||
               value.Contains("â€", StringComparison.Ordinal) ||
               value.Contains("â€¢", StringComparison.Ordinal) ||
               value.Contains("ä¸", StringComparison.Ordinal) ||
               value.Contains("æ—", StringComparison.Ordinal) ||
               value.Contains("ã", StringComparison.Ordinal) ||
               value.Contains("ç®", StringComparison.Ordinal) ||
               value.Contains("åˆ", StringComparison.Ordinal) ||
               value.Contains("KhÃ", StringComparison.Ordinal) ||
               value.Contains("Tiáº", StringComparison.Ordinal);
    }

    private static bool LooksLikeMojibakeFallback(string value)
    {
        return value.Contains('\u00E4') ||
               value.Contains('\u00E5') ||
               value.Contains('\u00E6') ||
               value.Contains('\u00E7') ||
               value.Contains('\u00E3');
    }

    private static bool TryGetWindows1252Bytes(string value, out byte[] bytes)
    {
        bytes = new byte[value.Length];

        for (var index = 0; index < value.Length; index++)
        {
            var mapped = value[index] switch
            {
                '\u20AC' => 0x80,
                '\u201A' => 0x82,
                '\u0192' => 0x83,
                '\u201E' => 0x84,
                '\u2026' => 0x85,
                '\u2020' => 0x86,
                '\u2021' => 0x87,
                '\u02C6' => 0x88,
                '\u2030' => 0x89,
                '\u0160' => 0x8A,
                '\u2039' => 0x8B,
                '\u0152' => 0x8C,
                '\u017D' => 0x8E,
                '\u2018' => 0x91,
                '\u2019' => 0x92,
                '\u201C' => 0x93,
                '\u201D' => 0x94,
                '\u2022' => 0x95,
                '\u2013' => 0x96,
                '\u2014' => 0x97,
                '\u02DC' => 0x98,
                '\u2122' => 0x99,
                '\u0161' => 0x9A,
                '\u203A' => 0x9B,
                '\u0153' => 0x9C,
                '\u017E' => 0x9E,
                '\u0178' => 0x9F,
                <= '\u00FF' => (int)value[index],
                _ => -1
            };

            if (mapped < 0)
            {
                bytes = Array.Empty<byte>();
                return false;
            }

            bytes[index] = (byte)mapped;
        }

        return true;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildLanguageMaps()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["vi"] = MergeMap(CreateVietnameseMap(), CreateVietnameseAuthOverrides()),
            ["en"] = MergeMap(CreateEnglishMap(), CreateEnglishAuthOverrides()),
            ["zh"] = MergeMap(CreateChineseMap(), CreateChineseAuthOverrides()),
            ["ja"] = MergeMap(CreateJapaneseMap(), CreateJapaneseAuthOverrides()),
            ["de"] = MergeMap(CreateGermanMap(), CreateGermanAuthOverrides())
        };
    }

    private static Dictionary<string, string> MergeMap(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> overrides)
    {
        var merged = new Dictionary<string, string>(source, StringComparer.Ordinal);

        foreach (var pair in overrides)
            merged[pair.Key] = pair.Value;

        return merged;
    }
}
