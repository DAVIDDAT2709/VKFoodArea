using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class PoiAudioAsset
{
    public int Id { get; set; }

    public int PoiId { get; set; }
    public Poi? Poi { get; set; }

    [Required, StringLength(10)]
    public string Language { get; set; } = "vi";

    [Required, StringLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
