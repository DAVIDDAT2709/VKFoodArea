using System.Text.Json;

namespace VKFoodArea.Web.Services;

public class TtsTranslationService : ITtsTranslationService
{
    private readonly HttpClient _httpClient;

    public TtsTranslationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TtsTranslationBundle> GenerateFromVietnameseAsync(string vietnameseText, CancellationToken ct = default)
    {
        var normalizedVietnamese = (vietnameseText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedVietnamese))
            return new TtsTranslationBundle(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        var english = await TranslateWithFallbackAsync(normalizedVietnamese, "en", ct);
        var chinese = await TranslateWithFallbackAsync(normalizedVietnamese, "zh-CN", ct);
        var japanese = await TranslateWithFallbackAsync(normalizedVietnamese, "ja", ct);
        var german = await TranslateWithFallbackAsync(normalizedVietnamese, "de", ct);

        return new TtsTranslationBundle(
            normalizedVietnamese,
            english,
            chinese,
            japanese,
            german);
    }

    private async Task<string> TranslateWithFallbackAsync(string vietnameseText, string targetLanguage, CancellationToken ct)
    {
        try
        {
            var requestUrl =
                "https://translate.googleapis.com/translate_a/single" +
                $"?client=gtx&sl=vi&tl={Uri.EscapeDataString(targetLanguage)}&dt=t&q={Uri.EscapeDataString(vietnameseText)}";

            using var response = await _httpClient.GetAsync(requestUrl, ct);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);

            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                return vietnameseText;

            var segments = json.RootElement[0];
            if (segments.ValueKind != JsonValueKind.Array)
                return vietnameseText;

            var translatedText = string.Concat(
                segments.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.Array && x.GetArrayLength() > 0)
                    .Select(x => x[0].GetString()));

            return string.IsNullOrWhiteSpace(translatedText)
                ? vietnameseText
                : translatedText.Trim();
        }
        catch
        {
            return vietnameseText;
        }
    }
}
