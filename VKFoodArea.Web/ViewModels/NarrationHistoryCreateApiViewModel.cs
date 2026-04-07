using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class NarrationHistoryCreateApiViewModel
{
    [Required]
    [Display(Name = "POI Id")]
    public int PoiId { get; set; }

    [Required]
    [StringLength(10)]
    [Display(Name = "Ngôn ngữ")]
    public string Language { get; set; } = "vi";

    [Required]
    [StringLength(20)]
    [Display(Name = "Nguồn phát")]
    public string TriggerSource { get; set; } = "manual";

    [Required]
    [StringLength(20)]
    [Display(Name = "Mode")]
    public string Mode { get; set; } = "tts";

    [Display(Name = "Thời gian phát")]
    public DateTime? PlayedAt { get; set; }
}
