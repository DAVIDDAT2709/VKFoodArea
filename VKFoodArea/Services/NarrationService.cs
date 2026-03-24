using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Media;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class NarrationService
{
    private readonly AppDbContext _db;
    private readonly AppLanguageService _languageService;
    private readonly AppSettingsService _settingsService;
    private readonly SemaphoreSlim _playLock = new(1, 1);
    private CancellationTokenSource? _ttsCts;
    private readonly NarrationSyncService _narrationSyncService;

    public NarrationService(
        AppDbContext db,
        AppLanguageService languageService,
        AppSettingsService settingsService,
        NarrationSyncService narrationSyncService)
    {
        _db = db;
        _languageService = languageService;
        _settingsService = settingsService;
        _narrationSyncService = narrationSyncService;
    }

    public async Task PlayPoiAsync(int poiId, CancellationToken ct = default)
    {
        await _playLock.WaitAsync(ct);

        try
        {
            var poi = await _db.Pois.FirstOrDefaultAsync(x => x.Id == poiId && x.IsActive, ct);
            if (poi is null)
                return;

            await StopInternalAsync();

            var language = _settingsService.NarrationLanguage;
            var mode = (_settingsService.NarrationOutputMode ?? "TTS").Trim();

            _languageService.CurrentLanguage = language;

            var script = GetScriptByLanguage(poi, language);

            if (string.IsNullOrWhiteSpace(script))
                return;

            _db.NarrationLogs.Add(new NarrationLog
            {
                PoiId = poiId,
                PlayedAt = DateTimeOffset.UtcNow,
                Mode = $"{mode}-{language}"
            });

            await _db.SaveChangesAsync(ct);

            await _narrationSyncService.PushHistoryAsync(
                    poi.Id,
                    poi.Name,
                    language,
                    mode,
                    "manual",
                    ct);

            _ttsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var locale = await TryGetLocaleAsync(language);

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Rate = 1.0f,
                Locale = locale
            };

            await TextToSpeech.Default.SpeakAsync(script, options, _ttsCts.Token);
        }
        finally
        {
            _playLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _playLock.WaitAsync();
        try
        {
            await StopInternalAsync();
        }
        finally
        {
            _playLock.Release();
        }
    }

    private async Task StopInternalAsync()
    {
        try
        {
            _ttsCts?.Cancel();
            _ttsCts?.Dispose();
            _ttsCts = null;
        }
        catch
        {
            await Task.CompletedTask;
        }
    }

    private static string GetScriptByLanguage(Poi poi, string language)
    {
        var normalized = AppLanguageService.NormalizeLanguage(language);

        return normalized switch
        {
            "en" when !string.IsNullOrWhiteSpace(poi.TtsScriptEn) => poi.TtsScriptEn,
            "zh" when !string.IsNullOrWhiteSpace(poi.TtsScriptZh) => poi.TtsScriptZh,
            "ja" when !string.IsNullOrWhiteSpace(poi.TtsScriptJa) => poi.TtsScriptJa,
            "de" when !string.IsNullOrWhiteSpace(poi.TtsScriptDe) => poi.TtsScriptDe,
            _ => poi.TtsScriptVi
        };
    }

    private static async Task<Locale?> TryGetLocaleAsync(string language)
    {
        try
        {
            var normalized = AppLanguageService.NormalizeLanguage(language);
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            return locales.FirstOrDefault(x =>
                x.Language.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}