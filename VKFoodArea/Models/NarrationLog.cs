namespace VKFoodArea.Models;

public class NarrationLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public int PoiId { get; set; }
    public int? TourId { get; set; }
    public string TourName { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = "manual";
    public DateTimeOffset PlayedAt { get; set; }
    public string Mode { get; set; } = string.Empty;
}
