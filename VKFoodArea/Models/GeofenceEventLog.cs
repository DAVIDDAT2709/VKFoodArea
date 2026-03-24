namespace VKFoodArea.Models;

public class GeofenceEventLog
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
}