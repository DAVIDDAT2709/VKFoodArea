namespace VKFoodArea.Models;

public class GeofenceDecision
{
    public bool ShouldTrigger { get; set; }
    public int? PoiId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
}