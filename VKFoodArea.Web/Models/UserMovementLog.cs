using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class UserMovementLog
{
    public int Id { get; set; }

    [StringLength(80)]
    public string UserKey { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }

    [Required, StringLength(20)]
    public string Source { get; set; } = "gps";

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
