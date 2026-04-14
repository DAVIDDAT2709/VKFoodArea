namespace VKFoodArea.Models;

public class TourSession
{
    public int TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string TourDescription { get; set; } = string.Empty;
    public string TtsScriptVi { get; set; } = string.Empty;
    public string TtsScriptEn { get; set; } = string.Empty;
    public string TtsScriptZh { get; set; } = string.Empty;
    public string TtsScriptJa { get; set; } = string.Empty;
    public string TtsScriptDe { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? IntroPlayedAt { get; set; }
    public string IntroPlayedLanguage { get; set; } = string.Empty;
    public bool IsFinished { get; set; }
    public int CurrentStopIndex { get; set; }
    public List<int> CompletedStopIds { get; set; } = new();
    public List<TourStop> Stops { get; set; } = new();

    public TourStop? CurrentStop
    {
        get
        {
            var orderedStops = OrderedStops;
            return IsFinished || CurrentStopIndex < 0 || CurrentStopIndex >= orderedStops.Count
                ? null
                : orderedStops[CurrentStopIndex];
        }
    }

    public IReadOnlyList<TourStop> OrderedStops
        => Stops
            .OrderBy(x => x.DisplayOrder)
            .ToList();
}
