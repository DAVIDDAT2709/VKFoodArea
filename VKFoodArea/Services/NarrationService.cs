using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using VKFoodArea.Data;
using VKFoodArea.Models;
#if ANDROID
using AndroidApp = Android.App.Application;
using AndroidBundle = Android.OS.Bundle;
using AndroidLocale = Java.Util.Locale;
using AndroidOperationResult = Android.Speech.Tts.OperationResult;
using AndroidQueueMode = Android.Speech.Tts.QueueMode;
using AndroidTextToSpeech = Android.Speech.Tts.TextToSpeech;
using AndroidUtteranceProgressListener = Android.Speech.Tts.UtteranceProgressListener;
using AndroidVoice = Android.Speech.Tts.Voice;
#endif

namespace VKFoodArea.Services;

public class NarrationService
{
    private readonly AppDbContext _db;
    private readonly AppLanguageService _languageService;
    private readonly AppSettingsService _settingsService;
    private readonly NarrationSyncService _narrationSyncService;

    private static readonly SemaphoreSlim _playLock = new(1, 1);
    private static CancellationTokenSource? _ttsCts;

#if ANDROID
    private static readonly SemaphoreSlim _androidTtsLock = new(1, 1);
    private static readonly object _utteranceSync = new();
    private static AndroidTextToSpeech? _androidTts;
    private static TaskCompletionSource<bool>? _activeUtteranceTcs;
    private static string? _activeUtteranceId;
#endif

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

    public async Task PlayPoiAsync(int poiId, string triggerSource = "manual", CancellationToken ct = default)
    {
        var poi = await _db.Pois.FirstOrDefaultAsync(x => x.Id == poiId && x.IsActive, ct);
        if (poi is null)
            return;

        var language = AppLanguageService.NormalizeLanguage(_settingsService.NarrationLanguage);
        var mode = (_settingsService.NarrationOutputMode ?? "TTS").Trim();

        _languageService.CurrentLanguage = language;

        var script = GetScriptByLanguage(poi, language);
        if (string.IsNullOrWhiteSpace(script))
            return;

        await LogNarrationAsync(poi, language, mode, triggerSource, ct);
        await SpeakTextAsync(script, language, ct);
    }

    public async Task PreviewAsync(string text, string language, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        await SpeakTextAsync(text, language, ct);
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? currentCts;

        await _playLock.WaitAsync();
        try
        {
            currentCts = _ttsCts;
            _ttsCts = null;
        }
        finally
        {
            _playLock.Release();
        }

        CancelPlayback(currentCts);
        StopPlatformPlayback();
    }

    private async Task LogNarrationAsync(
        Poi poi,
        string language,
        string mode,
        string triggerSource,
        CancellationToken ct)
    {
        _db.NarrationLogs.Add(new NarrationLog
        {
            PoiId = poi.Id,
            PlayedAt = DateTimeOffset.UtcNow,
            Mode = $"{mode}-{language}"
        });

        await _db.SaveChangesAsync(ct);

        await _narrationSyncService.PushHistoryAsync(
            poi.Id,
            poi.Name,
            language,
            mode,
            triggerSource,
            ct);
    }

    private async Task SpeakTextAsync(string text, string language, CancellationToken ct)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);

        CancellationTokenSource playbackCts;
        CancellationTokenSource? previousCts;

        await _playLock.WaitAsync(ct);
        try
        {
            playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            previousCts = _ttsCts;
            _ttsCts = playbackCts;
        }
        finally
        {
            _playLock.Release();
        }

        CancelPlayback(previousCts);

        try
        {
#if ANDROID
            await SpeakWithAndroidTtsAsync(text, normalizedLanguage, playbackCts.Token);
#else
            var locale = await TryGetLocaleAsync(normalizedLanguage);
            var options = BuildMauiSpeechOptions(locale, normalizedLanguage);
            await TextToSpeech.Default.SpeakAsync(text, options, playbackCts.Token);
#endif
        }
        catch (OperationCanceledException) when (playbackCts.IsCancellationRequested)
        {
        }
        finally
        {
            await _playLock.WaitAsync();
            try
            {
                if (ReferenceEquals(_ttsCts, playbackCts))
                    _ttsCts = null;
            }
            finally
            {
                _playLock.Release();
            }

            playbackCts.Dispose();
        }
    }

    private static SpeechOptions BuildMauiSpeechOptions(Locale? locale, string language)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);

        return new SpeechOptions
        {
            Pitch = normalizedLanguage == "vi" ? 0.90f : 1.0f,
            Volume = 1.0f,
            Rate = normalizedLanguage == "vi" ? 0.93f : 1.0f,
            Locale = locale
        };
    }

    private static void CancelPlayback(CancellationTokenSource? cts)
    {
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static void StopPlatformPlayback()
    {
#if ANDROID
        try
        {
            _androidTts?.Stop();
        }
        catch
        {
        }

        CompleteUtterance(null, canceled: true);
#endif
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

#if ANDROID
    private static async Task SpeakWithAndroidTtsAsync(string text, string language, CancellationToken ct)
    {
        var tts = await GetAndroidTtsAsync(ct);
        var profile = GetAndroidSpeechProfile(language);

        ConfigureAndroidVoice(tts, profile);

        var utteranceId = Guid.NewGuid().ToString("N");
        var utteranceTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        RegisterUtterance(utteranceId, utteranceTcs);

        using var cancellationRegistration = ct.Register(() =>
        {
            StopPlatformPlayback();
            CompleteUtterance(utteranceId, canceled: true);
        });

        var speakStatus = tts.Speak(text, AndroidQueueMode.Flush, (AndroidBundle?)null, utteranceId);

        if (speakStatus != AndroidOperationResult.Success)
        {
            CompleteUtterance(
                utteranceId,
                error: new InvalidOperationException($"Android TTS failed with status {speakStatus}."));
        }

        await utteranceTcs.Task.WaitAsync(ct);
    }

    private static async Task<AndroidTextToSpeech> GetAndroidTtsAsync(CancellationToken ct)
    {
        if (_androidTts is not null)
            return _androidTts;

        await _androidTtsLock.WaitAsync(ct);
        try
        {
            if (_androidTts is not null)
                return _androidTts;

            var initTcs = new TaskCompletionSource<AndroidOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var initListener = new AndroidTtsInitListener(initTcs);
            var context = Platform.CurrentActivity ?? AndroidApp.Context;
            var tts = new AndroidTextToSpeech(context, initListener);
            var initStatus = await initTcs.Task.WaitAsync(ct);

            if (initStatus != AndroidOperationResult.Success)
            {
                tts.Dispose();
                throw new InvalidOperationException($"Android TTS init failed with status {initStatus}.");
            }

            tts.SetOnUtteranceProgressListener(new NarrationUtteranceProgressListener());
            _androidTts = tts;
            return tts;
        }
        finally
        {
            _androidTtsLock.Release();
        }
    }

    private static void ConfigureAndroidVoice(AndroidTextToSpeech tts, AndroidSpeechProfile profile)
    {
        tts.SetSpeechRate(profile.Rate);
        tts.SetPitch(profile.Pitch);

        if (TrySetPreferredVoice(tts, profile))
            return;

        foreach (var locale in profile.PreferredLocales)
        {
            var availability = tts.SetLanguage(locale);
            if (availability is Android.Speech.Tts.LanguageAvailableResult.Available
                or Android.Speech.Tts.LanguageAvailableResult.CountryAvailable
                or Android.Speech.Tts.LanguageAvailableResult.CountryVarAvailable)
            {
                return;
            }
        }
    }

    private static bool TrySetPreferredVoice(AndroidTextToSpeech tts, AndroidSpeechProfile profile)
    {
        var voices = tts.Voices?
            .Where(x => x?.Locale is not null)
            .ToList();

        if (voices is null || voices.Count == 0)
            return false;

        var selectedVoice = voices
            .Select(voice => new
            {
                Voice = voice,
                Score = GetVoiceScore(voice!, profile)
            })
            .Where(x => x.Score > int.MinValue)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault()?
            .Voice;

        if (selectedVoice is null)
            return false;

        if (tts.SetVoice(selectedVoice) != AndroidOperationResult.Success)
            return false;

        tts.SetLanguage(selectedVoice.Locale);
        return true;
    }

    private static int GetVoiceScore(AndroidVoice voice, AndroidSpeechProfile profile)
    {
        var locale = voice.Locale;
        if (locale is null || string.IsNullOrWhiteSpace(locale.Language))
            return int.MinValue;

        var localeScore = profile.PreferredLocales
            .Select((preferredLocale, index) => GetLocaleScore(locale, preferredLocale) - (index * 5))
            .Max();

        if (localeScore < 0)
            return int.MinValue;

        var score = localeScore;

        if (!voice.IsNetworkConnectionRequired)
            score += 15;

        score += (int)voice.Quality;

        var voiceDescriptor = $"{voice.Name} {string.Join(" ", voice.Features ?? Array.Empty<string>())}".ToLowerInvariant();

        if (profile.PreferMaleVoice)
        {
            if (voiceDescriptor.Contains("male") || voiceDescriptor.Contains("man") || voiceDescriptor.Contains("m1") || voiceDescriptor.Contains("m2"))
                score += 40;

            if (voiceDescriptor.Contains("female") || voiceDescriptor.Contains("woman") || voiceDescriptor.Contains("f1") || voiceDescriptor.Contains("f2"))
                score -= 10;
        }

        return score;
    }

    private static int GetLocaleScore(AndroidLocale locale, AndroidLocale preferredLocale)
    {
        var localeLanguage = locale.Language?.ToLowerInvariant();
        var preferredLanguage = preferredLocale.Language?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(localeLanguage) || localeLanguage != preferredLanguage)
            return -1;

        var score = 100;

        var localeCountry = locale.Country?.ToLowerInvariant();
        var preferredCountry = preferredLocale.Country?.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(preferredCountry))
            score += localeCountry == preferredCountry ? 40 : -5;

        return score;
    }

    private static AndroidSpeechProfile GetAndroidSpeechProfile(string language)
    {
        return AppLanguageService.NormalizeLanguage(language) switch
        {
            "en" => new AndroidSpeechProfile(
                1.0f,
                1.0f,
                false,
                new AndroidLocale("en", "US"),
                new AndroidLocale("en", "GB")),
            "zh" => new AndroidSpeechProfile(
                1.0f,
                1.0f,
                false,
                new AndroidLocale("zh", "CN"),
                new AndroidLocale("zh", "TW")),
            "ja" => new AndroidSpeechProfile(
                1.0f,
                1.0f,
                false,
                new AndroidLocale("ja", "JP")),
            "de" => new AndroidSpeechProfile(
                0.98f,
                0.98f,
                false,
                new AndroidLocale("de", "DE")),
            _ => new AndroidSpeechProfile(
                0.93f,
                0.90f,
                true,
                new AndroidLocale("vi", "VN"))
        };
    }

    private static void RegisterUtterance(string utteranceId, TaskCompletionSource<bool> utteranceTcs)
    {
        lock (_utteranceSync)
        {
            _activeUtteranceId = utteranceId;
            _activeUtteranceTcs = utteranceTcs;
        }
    }

    private static void CompleteUtterance(string? utteranceId, Exception? error = null, bool canceled = false)
    {
        TaskCompletionSource<bool>? utteranceTcs = null;

        lock (_utteranceSync)
        {
            if (_activeUtteranceTcs is null)
                return;

            if (utteranceId is not null &&
                !string.Equals(_activeUtteranceId, utteranceId, StringComparison.Ordinal))
            {
                return;
            }

            utteranceTcs = _activeUtteranceTcs;
            _activeUtteranceTcs = null;
            _activeUtteranceId = null;
        }

        if (canceled)
        {
            utteranceTcs.TrySetCanceled();
            return;
        }

        if (error is not null)
        {
            utteranceTcs.TrySetException(error);
            return;
        }

        utteranceTcs.TrySetResult(true);
    }

    private sealed record AndroidSpeechProfile(
        float Rate,
        float Pitch,
        bool PreferMaleVoice,
        params AndroidLocale[] PreferredLocales);

    private sealed class AndroidTtsInitListener(TaskCompletionSource<AndroidOperationResult> initTcs)
        : Java.Lang.Object, AndroidTextToSpeech.IOnInitListener
    {
        private readonly TaskCompletionSource<AndroidOperationResult> _initTcs = initTcs;

        public void OnInit(AndroidOperationResult status)
        {
            _initTcs.TrySetResult(status);
        }
    }

    private sealed class NarrationUtteranceProgressListener : AndroidUtteranceProgressListener
    {
        public override void OnStart(string? utteranceId)
        {
        }

        public override void OnDone(string? utteranceId)
        {
            CompleteUtterance(utteranceId);
        }

        public override void OnError(string? utteranceId)
        {
            CompleteUtterance(
                utteranceId,
                new InvalidOperationException("Android TTS synthesis failed."));
        }

        public override void OnStop(string? utteranceId, bool interrupted)
        {
            CompleteUtterance(utteranceId, canceled: true);
        }
    }
#endif

    private static async Task<Locale?> TryGetLocaleAsync(string language)
    {
        try
        {
            var normalized = AppLanguageService.NormalizeLanguage(language);
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            return locales.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Language) &&
                x.Language.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}
