namespace VKFoodArea.Models;

public class MovementLogPushDto
{
    public string UserKey { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public string Source { get; set; } = "gps";
    public DateTime? RecordedAt { get; set; }
}
