using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class NarrationHistory
{
    public int Id { get; set; }

    public int PoiId { get; set; }
    public Poi? Poi { get; set; }

    public int? TourId { get; set; }

    [StringLength(120)]
    public string TourName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string PoiName { get; set; } = string.Empty;

    [StringLength(80)]
    public string UserKey { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Language { get; set; } = "vi";

    [Required, StringLength(20)]
    public string TriggerSource { get; set; } = "manual";

    [Required, StringLength(20)]
    public string Mode { get; set; } = "tts";

    public int? DurationSeconds { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime PlayedAt { get; set; } = DateTime.Now;
}
