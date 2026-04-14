using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class Tour
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public string TtsScriptVi { get; set; } = string.Empty;
    public string TtsScriptEn { get; set; } = string.Empty;
    public string TtsScriptZh { get; set; } = string.Empty;
    public string TtsScriptJa { get; set; } = string.Empty;
    public string TtsScriptDe { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TourStop> Stops { get; set; } = new();
}
