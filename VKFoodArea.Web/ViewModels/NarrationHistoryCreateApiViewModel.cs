using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class NarrationHistoryCreateApiViewModel
{
    [Required]
    [Display(Name = "POI Id")]
    public int PoiId { get; set; }

    [StringLength(120)]
    [Display(Name = "Ten POI")]
    public string? PoiName { get; set; }

    [StringLength(100)]
    [Display(Name = "Ma QR")]
    public string? QrCode { get; set; }

    [Required]
    [StringLength(10)]
    [Display(Name = "Ngon ngu")]
    public string Language { get; set; } = "vi";

    [Required]
    [StringLength(20)]
    [Display(Name = "Nguon phat")]
    public string TriggerSource { get; set; } = "manual";

    [Required]
    [StringLength(20)]
    [Display(Name = "Mode")]
    public string Mode { get; set; } = "tts";

    [Display(Name = "Thoi gian phat")]
    public DateTime? PlayedAt { get; set; }
}
