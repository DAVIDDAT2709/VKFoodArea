using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VKFoodArea.Web.ViewModels;

public class QrCodeItemFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã QR.")]
    [StringLength(120)]
    [Display(Name = "Mã QR")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề.")]
    [StringLength(120)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn POI.")]
    [Display(Name = "POI")]
    public int PoiId { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public List<SelectListItem> PoiOptions { get; set; } = new();
}
