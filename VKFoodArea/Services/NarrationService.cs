using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Plugin.Maui.Audio;
using VKFoodArea.Data;
using VKFoodArea.Models;

#if ANDROID
using AndroidApp = Android.App.Application;
using AndroidBundle = Android.OS.Bundle;
using AndroidLocale = Java.Util.Locale;
using AndroidTextToSpeechError = Android.Speech.Tts.TextToSpeechError;
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
    private readonly NarrationUiStateService _uiState;
    private readonly AuthService _authService;
    private readonly IAudioManager _audioManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    private static readonly SemaphoreSlim _playLock = new(1, 1);
    private static CancellationTokenSource? _ttsCts;
    private static AsyncAudioPlayer? _activeAudioPlayer;
    private static MemoryStream? _activeAudioBuffer;

    private static readonly object _requestLock = new();
    private static CancellationTokenSource? _requestCts;
    private static int? _currentPoiId;

    // chống spam cùng một quán trong 5 giây
    private static readonly TimeSpan SwitchPoiDelay = TimeSpan.FromSeconds(3);

#if ANDROID
    private static readonly SemaphoreSlim _androidTtsLock = new(1, 1);
    private static readonly object _utteranceSync = new();
    private static AndroidTextToSpeech? _androidTts;
    private static TaskCompletionSource<bool>? _activeUtteranceTcs;
    private static string? _activeUtteranceId;
#endif

    public static event EventHandler<NarrationPlaybackStateChangedEventArgs>? PlaybackStateChanged;

    public NarrationService(
        AppDbContext db,
        AppLanguageService languageService,
        AppSettingsService settingsService,
        NarrationSyncService narrationSyncService,
        NarrationUiStateService uiState,
        AuthService authService,
        IAudioManager audioManager,
        IHttpClientFactory httpClientFactory,
        ApiBaseUrlService apiBaseUrlService)
    {
        _db = db;
        _languageService = languageService;
        _settingsService = settingsService;
        _narrationSyncService = narrationSyncService;
        _uiState = uiState;
        _authService = authService;
        _audioManager = audioManager;
        _httpClientFactory = httpClientFactory;
        _apiBaseUrlService = apiBaseUrlService;
    }

    public async Task PlayPoiAsync(
        int poiId,
        string triggerSource = "manual",
        string? overrideLanguage = null,
        string? overrideMode = null,
        CancellationToken ct = default)
    {
        var poi = await _db.Pois.FirstOrDefaultAsync(x => x.Id == poiId && x.IsActive, ct);
        if (poi is null)
            return;

        await PlayPoiAsync(poi, triggerSource, overrideLanguage, overrideMode, ct);
    }

    public async Task PlayPoiAsync(
        Poi poi,
        string triggerSource = "manual",
        string? overrideLanguage = null,
        string? overrideMode = null,
        CancellationToken ct = default)
    {
        if (poi is null || !poi.IsActive)
            return;

        // Chỉ chặn spam khi bấm lại cùng một quán trong 5 giây
        if (IsCurrentPoiPlaying(poi.Id))
            return;

        var requestCts = ReplaceRequestToken(ct);
        var requestToken = requestCts.Token;

        try
        {
            // Nếu đang phát quán khác thì dừng ngay và chờ 3 giây trước khi phát quán mới
            if (_currentPoiId.HasValue && _currentPoiId.Value != poi.Id)
            {
                await StopCurrentPlaybackOnlyAsync();
                await Task.Delay(SwitchPoiDelay, requestToken);
            }
            // Nếu đang là chính quán này và không bị chặn spam thì vẫn bỏ qua để không phát chồng
            else if (_currentPoiId.HasValue && _currentPoiId.Value == poi.Id)
            {
                return;
            }

            requestToken.ThrowIfCancellationRequested();

            var language = AppLanguageService.NormalizeLanguage(
                string.IsNullOrWhiteSpace(overrideLanguage)
                    ? _settingsService.NarrationLanguage
                    : overrideLanguage);
            var mode = NormalizePlaybackMode(
                string.IsNullOrWhiteSpace(overrideMode)
                    ? _settingsService.NarrationOutputMode
                    : overrideMode);

            _languageService.CurrentLanguage = language;

            var playbackPlan = ResolvePlaybackPlan(poi, language, mode);
            if (playbackPlan is null)
                return;

            _currentPoiId = poi.Id;
            _uiState.SetPlayback(true, poi, playbackPlan.EffectiveMode, playbackPlan.SpokenLanguage);
            PublishPlaybackState(true, poi.Name, playbackPlan.EffectiveMode, playbackPlan.SpokenLanguage);

            await LogNarrationAsync(poi, playbackPlan.SpokenLanguage, playbackPlan.EffectiveMode, triggerSource, requestToken);
            await PlayResolvedPlanAsync(playbackPlan, requestToken);
        }
        catch (OperationCanceledException)
        {
            // Người dùng chọn quán khác hoặc bấm dừng -> bỏ qua, không crash
        }
        finally
        {
            lock (_requestLock)
            {
                if (ReferenceEquals(_requestCts, requestCts))
                    _requestCts = null;
            }

            requestCts.Dispose();

            var shouldClearPlaybackState = false;

            if (_currentPoiId == poi.Id)
            {
                _currentPoiId = null;
                shouldClearPlaybackState = true;
            }

            if (shouldClearPlaybackState)
            {
                _uiState.SetPlayback(false, poi, mode: null, language: null);
                PublishPlaybackState(false, poi.Name, null, null);
            }
        }
    }

    public async Task PreviewAsync(
        string text,
        string language,
        string? playbackMode = null,
        CancellationToken ct = default)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        var normalizedMode = NormalizePlaybackMode(playbackMode);
        var previewAudioSource = ResolvePreviewAudioSource(normalizedLanguage, normalizedMode);

        if (previewAudioSource is not null)
        {
            await PlayAudioAsync(previewAudioSource, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
            return;

        await SpeakTextAsync(text, normalizedLanguage, ct);
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? requestToCancel = null;

        lock (_requestLock)
        {
            requestToCancel = _requestCts;
            _requestCts = null;
            _currentPoiId = null;
        }

        if (requestToCancel is not null)
        {
            try
            {
                requestToCancel.Cancel();
            }
            catch
            {
            }
            finally
            {
                requestToCancel.Dispose();
            }
        }

        await StopCurrentPlaybackOnlyAsync();
        _uiState.SetPlayback(false, poi: null, mode: null, language: null);
        PublishPlaybackState(false, null, null, null);
    }

    private static CancellationTokenSource ReplaceRequestToken(CancellationToken externalToken = default)
    {
        CancellationTokenSource? oldCts = null;
        CancellationTokenSource newCts;

        lock (_requestLock)
        {
            oldCts = _requestCts;
            newCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            _requestCts = newCts;
        }

        if (oldCts is not null)
        {
            try
            {
                oldCts.Cancel();
            }
            catch
            {
            }
            finally
            {
                oldCts.Dispose();
            }
        }

        return newCts;
    }

    private static bool IsCurrentPoiPlaying(int poiId)
    {
        lock (_requestLock)
        {
            return _currentPoiId == poiId && _requestCts is not null;
        }
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
            UserId = _authService.GetCurrentUserId(),
            PoiId = poi.Id,
            PlayedAt = DateTimeOffset.UtcNow,
            Mode = $"{mode}-{language}"
        });

        await _db.SaveChangesAsync(ct);

        await _narrationSyncService.PushHistoryAsync(
            poi.Id,
            poi.Name,
            poi.QrCode,
            language,
            mode,
            _authService.GetCurrentUserSyncKey(),
            triggerSource,
            ct);
    }

    private async Task StopCurrentPlaybackOnlyAsync()
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

    private async Task PlayResolvedPlanAsync(ResolvedPlaybackPlan playbackPlan, CancellationToken ct)
    {
        if (playbackPlan.Kind == NarrationPlaybackKind.Audio && playbackPlan.AudioSource is not null)
        {
            await PlayAudioAsync(playbackPlan.AudioSource, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(playbackPlan.Script))
            return;

        await SpeakTextAsync(playbackPlan.Script, playbackPlan.SpokenLanguage, ct);
    }

    private async Task PlayAudioAsync(ResolvedAudioSource audioSource, CancellationToken ct)
    {
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
        StopPlatformPlayback();

        var buffer = await LoadAudioBufferAsync(audioSource.SourcePath, ct);
        if (buffer is null)
            throw new InvalidOperationException($"Audio source is unavailable: {audioSource.SourcePath}");

        AsyncAudioPlayer? player = null;
        var ownsBuffer = true;

        try
        {
            player = _audioManager.CreateAsyncPlayer(buffer);

            await _playLock.WaitAsync(ct);
            try
            {
                _activeAudioPlayer = player;
                _activeAudioBuffer = buffer;
                ownsBuffer = false;
            }
            finally
            {
                _playLock.Release();
            }

            await player.PlayAsync(playbackCts.Token);
        }
        finally
        {
            MemoryStream? bufferToDispose = null;

            await _playLock.WaitAsync();
            try
            {
                if (ReferenceEquals(_ttsCts, playbackCts))
                    _ttsCts = null;

                if (ReferenceEquals(_activeAudioPlayer, player))
                {
                    _activeAudioPlayer = null;
                    bufferToDispose = _activeAudioBuffer;
                    _activeAudioBuffer = null;
                }
            }
            finally
            {
                _playLock.Release();
            }

            player?.Dispose();
            bufferToDispose?.Dispose();

            if (ownsBuffer)
                buffer.Dispose();

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
        try
        {
            _activeAudioPlayer?.Stop();
            _activeAudioPlayer?.Dispose();
        }
        catch
        {
        }
        finally
        {
            _activeAudioPlayer = null;
            _activeAudioBuffer?.Dispose();
            _activeAudioBuffer = null;
        }

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

    private static ResolvedNarrationContent? ResolveNarrationContent(Poi poi, string language)
    {
        var normalized = AppLanguageService.NormalizeLanguage(language);
        var vietnameseScript = NormalizeScript(poi.TtsScriptVi);

        if (normalized == "vi")
        {
            return string.IsNullOrWhiteSpace(vietnameseScript)
                ? null
                : new ResolvedNarrationContent(vietnameseScript, "vi");
        }

        var translatedScript = normalized switch
        {
            "en" => NormalizeScript(poi.TtsScriptEn),
            "zh" => NormalizeScript(poi.TtsScriptZh),
            "ja" => NormalizeScript(poi.TtsScriptJa),
            "de" => NormalizeScript(poi.TtsScriptDe),
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(translatedScript) &&
            !string.Equals(translatedScript, vietnameseScript, StringComparison.Ordinal))
        {
            return new ResolvedNarrationContent(translatedScript, normalized);
        }

        if (!string.IsNullOrWhiteSpace(vietnameseScript))
            return new ResolvedNarrationContent(vietnameseScript, "vi");

        return string.IsNullOrWhiteSpace(translatedScript)
            ? null
            : new ResolvedNarrationContent(translatedScript, normalized);
    }

    private ResolvedPlaybackPlan? ResolvePlaybackPlan(Poi poi, string language, string mode)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        var normalizedMode = NormalizePlaybackMode(mode);
        var audioSource = ResolveAudioSource(poi, normalizedLanguage);
        var narrationContent = ResolveNarrationContent(poi, normalizedLanguage);

        return normalizedMode switch
        {
            "Audio" => audioSource is not null
                ? new ResolvedPlaybackPlan(
                    NarrationPlaybackKind.Audio,
                    "Audio",
                    audioSource.SpokenLanguage,
                    null,
                    audioSource)
                : narrationContent is null
                    ? null
                    : new ResolvedPlaybackPlan(
                        NarrationPlaybackKind.Tts,
                        "TTS",
                        narrationContent.SpokenLanguage,
                        narrationContent.Script,
                        null),
            "Auto" => audioSource is not null
                ? new ResolvedPlaybackPlan(
                    NarrationPlaybackKind.Audio,
                    "Audio",
                    audioSource.SpokenLanguage,
                    null,
                    audioSource)
                : narrationContent is null
                    ? null
                    : new ResolvedPlaybackPlan(
                        NarrationPlaybackKind.Tts,
                        "TTS",
                        narrationContent.SpokenLanguage,
                        narrationContent.Script,
                        null),
            _ => narrationContent is not null
                ? new ResolvedPlaybackPlan(
                    NarrationPlaybackKind.Tts,
                    "TTS",
                    narrationContent.SpokenLanguage,
                    narrationContent.Script,
                    null)
                : audioSource is null
                    ? null
                    : new ResolvedPlaybackPlan(
                        NarrationPlaybackKind.Audio,
                        "Audio",
                        audioSource.SpokenLanguage,
                        null,
                        audioSource)
        };
    }

    private ResolvedAudioSource? ResolvePreviewAudioSource(string language, string playbackMode)
    {
        _ = language;
        _ = playbackMode;
        return null;
    }

    private ResolvedAudioSource? ResolveAudioSource(Poi poi, string language)
    {
        var audioPath = AppLanguageService.NormalizeLanguage(language) switch
        {
            "vi" => NormalizeAudioPath(poi.AudioFileVi),
            "en" => NormalizeAudioPath(poi.AudioFileEn),
            "ja" => NormalizeAudioPath(poi.AudioFileJa),
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(audioPath))
            return null;

        return new ResolvedAudioSource(audioPath, AppLanguageService.NormalizeLanguage(language));
    }

    private async Task<MemoryStream?> LoadAudioBufferAsync(string audioPath, CancellationToken ct)
    {
        var normalizedPath = NormalizeAudioPath(audioPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return null;

        if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri) &&
            (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            using var response = await _httpClientFactory.CreateClient().GetAsync(absoluteUri, ct);
            response.EnsureSuccessStatusCode();
            await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
            return await CopyToMemoryAsync(sourceStream, ct);
        }

        if (Path.IsPathRooted(normalizedPath) && File.Exists(normalizedPath))
        {
            await using var fileStream = File.OpenRead(normalizedPath);
            return await CopyToMemoryAsync(fileStream, ct);
        }

        var mediaUrl = ResolveRelativeAudioUrl(normalizedPath);
        if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out var mediaUri) &&
            (string.Equals(mediaUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(mediaUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            using var response = await _httpClientFactory.CreateClient().GetAsync(mediaUri, ct);
            if (response.IsSuccessStatusCode)
            {
                await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
                return await CopyToMemoryAsync(sourceStream, ct);
            }
        }

        try
        {
            await using var packageStream = await FileSystem.OpenAppPackageFileAsync(
                NormalizePackageAudioPath(normalizedPath));
            return await CopyToMemoryAsync(packageStream, ct);
        }
        catch
        {
            return null;
        }
    }

    private string ResolveRelativeAudioUrl(string audioPath)
    {
        if (audioPath.StartsWith("/", StringComparison.Ordinal))
            return new Uri(new Uri(_apiBaseUrlService.BaseUrl), audioPath.TrimStart('/')).ToString();

        if (audioPath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return new Uri(new Uri(_apiBaseUrlService.BaseUrl), audioPath).ToString();

        return audioPath;
    }

    private static async Task<MemoryStream> CopyToMemoryAsync(Stream sourceStream, CancellationToken ct)
    {
        var memoryStream = new MemoryStream();
        await sourceStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static string NormalizePackageAudioPath(string audioPath)
    {
        var normalizedPath = NormalizeAudioPath(audioPath);
        if (normalizedPath.StartsWith("Resources/Raw/", StringComparison.OrdinalIgnoreCase))
            return normalizedPath["Resources/Raw/".Length..];

        if (normalizedPath.StartsWith("Resources\\Raw\\", StringComparison.OrdinalIgnoreCase))
            return normalizedPath["Resources\\Raw\\".Length..];

        return normalizedPath.TrimStart('/', '\\');
    }

    private static string NormalizeScript(string? script)
        => (script ?? string.Empty).Trim();

    private static string NormalizeAudioPath(string? audioPath)
        => (audioPath ?? string.Empty).Trim();

    private static string NormalizePlaybackMode(string? mode)
    {
        return (mode ?? "TTS").Trim() switch
        {
            "Auto" => "Auto",
            "Audio" => "Audio",
            _ => "TTS"
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
                0.92f,
                0.96f,
                false,
                CreateAndroidLocale("en-US"),
                CreateAndroidLocale("en-GB")),
            "zh" => new AndroidSpeechProfile(
                0.90f,
                0.96f,
                false,
                CreateAndroidLocale("zh-CN"),
                CreateAndroidLocale("zh-TW")),
            "ja" => new AndroidSpeechProfile(
                0.90f,
                0.96f,
                false,
                CreateAndroidLocale("ja-JP")),
            "de" => new AndroidSpeechProfile(
                0.90f,
                0.96f,
                false,
                CreateAndroidLocale("de-DE")),
            _ => new AndroidSpeechProfile(
                0.82f,
                0.94f,
                false,
                CreateAndroidLocale("vi-VN"))
        };
    }

    private static AndroidLocale CreateAndroidLocale(string languageTag)
        => AndroidLocale.ForLanguageTag(languageTag);

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

#pragma warning disable CS0672
        [Obsolete]
        public override void OnError(string? utteranceId)
        {
            CompleteUtterance(
                utteranceId,
                new InvalidOperationException("Android TTS synthesis failed."));
        }
#pragma warning restore CS0672

        public override void OnError(string? utteranceId, AndroidTextToSpeechError errorCode)
        {
            CompleteUtterance(
                utteranceId,
                new InvalidOperationException($"Android TTS synthesis failed with error {errorCode}."));
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

    private static void PublishPlaybackState(bool isPlaying, string? poiName, string? mode, string? language)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlaybackStateChanged?.Invoke(
                null,
                new NarrationPlaybackStateChangedEventArgs(isPlaying, poiName, mode, language));
        });
    }

    private sealed record ResolvedNarrationContent(string Script, string SpokenLanguage);
    private sealed record ResolvedAudioSource(string SourcePath, string SpokenLanguage);
    private sealed record ResolvedPlaybackPlan(
        NarrationPlaybackKind Kind,
        string EffectiveMode,
        string SpokenLanguage,
        string? Script,
        ResolvedAudioSource? AudioSource);
}

public enum NarrationPlaybackKind
{
    Tts,
    Audio
}

public sealed class NarrationPlaybackStateChangedEventArgs : EventArgs
{
    public NarrationPlaybackStateChangedEventArgs(bool isPlaying, string? poiName, string? mode, string? language)
    {
        IsPlaying = isPlaying;
        PoiName = poiName;
        Mode = mode;
        Language = language;
    }

    public bool IsPlaying { get; }
    public string? PoiName { get; }
    public string? Mode { get; }
    public string? Language { get; }
}
