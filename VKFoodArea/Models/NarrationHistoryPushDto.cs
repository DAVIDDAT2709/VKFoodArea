namespace VKFoodArea.Models;

public class NarrationHistoryPushDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int? TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string TriggerSource { get; set; } = "manual";
    public string Mode { get; set; } = "tts";
    public DateTime? PlayedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
