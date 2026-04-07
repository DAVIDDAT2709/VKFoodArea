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
    public string? PhoneNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "Ảnh / URL ảnh")]
    public string? ImageUrl { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude không hợp lệ.")]
    [Display(Name = "Latitude")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude không hợp lệ.")]
    [Display(Name = "Longitude")]
    public double Longitude { get; set; }

    [Range(1, 500, ErrorMessage = "Bán kính nên nằm trong khoảng 1-500m.")]
    [Display(Name = "Bán kính geofence (m)")]
    public double RadiusMeters { get; set; } = 30;

    [Required(ErrorMessage = "Vui lòng nhập TTS tiếng Việt.")]
    [Display(Name = "TTS tiếng Việt")]
    public string TtsScriptVi { get; set; } = string.Empty;

    [Display(Name = "TTS English")]
    public string? TtsScriptEn { get; set; }

    [Display(Name = "TTS 中文")]
    public string? TtsScriptZh { get; set; }

    [Display(Name = "TTS 日本語")]
    public string? TtsScriptJa { get; set; }

    [Display(Name = "TTS Deutsch")]
    public string? TtsScriptDe { get; set; }

    [StringLength(100)]
    [Display(Name = "Mã QR mặc định")]
    public string? QrCode { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}
