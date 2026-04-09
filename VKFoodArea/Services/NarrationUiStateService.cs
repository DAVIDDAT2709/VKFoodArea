using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class NarrationUiStateService
{
    public event EventHandler? StateChanged;

    public bool HasContext { get; private set; }
    public bool IsPlaying { get; private set; }
    public string PoiName { get; private set; } = string.Empty;
    public string ImageUrl { get; private set; } = string.Empty;
    public string Language { get; private set; } = "vi";
    public string Mode { get; private set; } = "TTS";

    public void SetContext(Poi poi, string? mode = null, string? language = null)
    {
        PoiName = poi.Name;
        ImageUrl = poi.ImageUrl;
        HasContext = true;

        if (!string.IsNullOrWhiteSpace(mode))
            Mode = mode.Trim();

        if (!string.IsNullOrWhiteSpace(language))
            Language = AppLanguageService.NormalizeLanguage(language);

        NotifyChanged();
    }

    public void SetPlayback(bool isPlaying, Poi? poi, string? mode, string? language)
    {
        if (poi is not null)
        {
            PoiName = poi.Name;
            ImageUrl = poi.ImageUrl;
            HasContext = true;
        }

        if (!string.IsNullOrWhiteSpace(mode))
            Mode = mode.Trim();

        if (!string.IsNullOrWhiteSpace(language))
            Language = AppLanguageService.NormalizeLanguage(language);

        IsPlaying = isPlaying;

        if (!HasContext && !string.IsNullOrWhiteSpace(PoiName))
            HasContext = true;

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
