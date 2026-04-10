namespace VKFoodArea.Models;

public class NarrationLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public int PoiId { get; set; }
    public DateTimeOffset PlayedAt { get; set; }
    public string Mode { get; set; } = string.Empty;
}
