using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class PoiFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên địa điểm.")]
    [StringLength(120)]
    [Display(Name = "Tên địa điểm")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ.")]
    [StringLength(250)]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Ảnh / URL ảnh")]
    public string ImageUrl { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Latitude không hợp lệ.")]
    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude không hợp lệ.")]
    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Range(1, 500, ErrorMessage = "Bán kính nên nằm trong khoảng 1-500m.")]
    [Display(Name = "Bán kính geofence (m)")]
    public double RadiusMeters { get; set; } = 30;

    [StringLength(300)]
    [Display(Name = "Mô tả ngắn")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Script tiếng Việt")]
    public string TtsScriptVi { get; set; } = string.Empty;

    [Display(Name = "Script tiếng Anh")]
    public string TtsScriptEn { get; set; } = string.Empty;

    [Display(Name = "Script 中文")]
    public string TtsScriptZh { get; set; } = string.Empty;

    [Display(Name = "Script 日本語")]
    public string TtsScriptJa { get; set; } = string.Empty;

    [Display(Name = "Script Deutsch")]
    public string TtsScriptDe { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Mã QR mặc định")]
    public string QrCode { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}