using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class TourNarrationService
{
    private readonly NarrationService _narrationService;
    private readonly AppSettingsService _appSettingsService;

    public TourNarrationService(
        NarrationService narrationService,
        AppSettingsService appSettingsService)
    {
        _narrationService = narrationService;
        _appSettingsService = appSettingsService;
    }

    public string CurrentLanguage
        => AppLanguageService.NormalizeLanguage(_appSettingsService.NarrationLanguage);

    public async Task PlayIntroAsync(TourSession session, CancellationToken ct = default)
    {
        var language = CurrentLanguage;
        var script = ResolveScript(session, language);

        if (string.IsNullOrWhiteSpace(script))
            return;

        await _narrationService.StopAsync();
        await _narrationService.PreviewAsync(script, language, "TTS", ct);
    }

    public string ResolveDisplaySummary(Tour tour)
    {
        var language = CurrentLanguage;
        var translatedScript = ResolveScript(
            tour.TtsScriptVi,
            tour.TtsScriptEn,
            tour.TtsScriptZh,
            tour.TtsScriptJa,
            tour.TtsScriptDe,
            language);

        if (language == "vi" && !string.IsNullOrWhiteSpace(tour.Description))
            return tour.Description.Trim();

        if (!string.IsNullOrWhiteSpace(translatedScript))
            return translatedScript;

        if (!string.IsNullOrWhiteSpace(tour.Description))
            return tour.Description.Trim();

        return tour.Name.Trim();
    }

    public string ResolveDisplaySummary(TourSession session)
    {
        var language = CurrentLanguage;
        var translatedScript = ResolveScript(session, language);

        if (language == "vi" && !string.IsNullOrWhiteSpace(session.TourDescription))
            return session.TourDescription.Trim();

        if (!string.IsNullOrWhiteSpace(translatedScript))
            return translatedScript;

        if (!string.IsNullOrWhiteSpace(session.TourDescription))
            return session.TourDescription.Trim();

        return session.TourName.Trim();
    }

    private static string ResolveScript(TourSession session, string language)
        => ResolveScript(
            session.TtsScriptVi,
            session.TtsScriptEn,
            session.TtsScriptZh,
            session.TtsScriptJa,
            session.TtsScriptDe,
            language);

    private static string ResolveScript(
        string? vi,
        string? en,
        string? zh,
        string? ja,
        string? de,
        string language)
    {
        var script = language switch
        {
            "en" => en,
            "zh" => zh,
            "ja" => ja,
            "de" => de,
            _ => vi
        };

        if (!string.IsNullOrWhiteSpace(script))
            return script.Trim();

        if (!string.IsNullOrWhiteSpace(vi))
            return vi.Trim();

        return string.Empty;
    }
}
