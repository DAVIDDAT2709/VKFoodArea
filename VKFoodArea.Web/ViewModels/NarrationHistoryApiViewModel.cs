namespace VKFoodArea.Web.ViewModels;

public class NarrationHistoryApiViewModel
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int? TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
