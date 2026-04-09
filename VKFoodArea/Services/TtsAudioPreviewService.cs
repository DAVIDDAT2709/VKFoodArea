namespace VKFoodArea.Services;

public class TtsAudioPreviewService
{
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;

    public TtsAudioPreviewService(
        NarrationService narrationService,
        AppTextService text)
    {
        _narrationService = narrationService;
        _text = text;
    }

    public Task PlayPreviewAsync(
        string? language,
        string? playbackMode,
        CancellationToken ct = default)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        _ = SoundSettingsService.NormalizePlaybackMode(playbackMode);

        return _narrationService.PreviewAsync(
            _text.GetPreviewText(normalizedLanguage),
            normalizedLanguage,
            ct);
    }
}
