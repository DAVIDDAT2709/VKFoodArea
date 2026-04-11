using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class PoiTranslation
{
    public int Id { get; set; }

    public int PoiId { get; set; }
    public Poi? Poi { get; set; }

    [Required, StringLength(10)]
    public string Language { get; set; } = "vi";

    public string Script { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
