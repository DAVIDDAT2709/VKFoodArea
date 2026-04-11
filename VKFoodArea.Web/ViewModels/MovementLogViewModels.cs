using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class MovementLogCreateApiViewModel
{
    [StringLength(80)]
    public string? UserKey { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Range(0, 10000)]
    public double? AccuracyMeters { get; set; }

    [StringLength(20)]
    public string Source { get; set; } = "gps";

    public DateTime? RecordedAt { get; set; }
}

public class MovementLogApiViewModel
{
    public int Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public string Source { get; set; } = "gps";
    public DateTime RecordedAt { get; set; }
}
