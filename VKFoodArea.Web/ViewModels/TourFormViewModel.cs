using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VKFoodArea.Web.ViewModels;

public class TourFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên tour.")]
    [StringLength(120)]
    [Display(Name = "Tên tour")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public List<TourStopInputViewModel> Stops { get; set; } = new();
    public List<SelectListItem> PoiOptions { get; set; } = new();
}

public class TourStopInputViewModel
{
    public int? Id { get; set; }

    [Display(Name = "POI")]
    public int? PoiId { get; set; }

    [Display(Name = "Thứ tự")]
    public int DisplayOrder { get; set; }

    [StringLength(300)]
    [Display(Name = "Ghi chú")]
    public string Note { get; set; } = string.Empty;
}
