using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

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
    public string? ImageUrl { get; set; }

    [Display(Name = "Ảnh từ máy tính")]
    public IFormFile? ImageFile { get; set; }

    [Range(-90, 90, ErrorMessage = "Vĩ độ không hợp lệ.")]
    [Display(Name = "Vĩ độ")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Kinh độ không hợp lệ.")]
    [Display(Name = "Kinh độ")]
    public double Longitude { get; set; }

    [Range(1, 500, ErrorMessage = "Bán kính nên nằm trong khoảng 1-500m.")]
    [Display(Name = "Bán kính (m)")]
    public double RadiusMeters { get; set; } = 30;

    [Range(1, 100, ErrorMessage = "Ưu tiên nên nằm trong khoảng 1-100.")]
    [Display(Name = "Ưu tiên")]
    public int Priority { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng nhập TTS tiếng Việt.")]
    [Display(Name = "TTS tiếng Việt")]
    public string TtsScriptVi { get; set; } = string.Empty;

    [Display(Name = "TTS tiếng Anh")]
    public string? TtsScriptEn { get; set; }

    [Display(Name = "TTS tiếng Trung")]
    public string? TtsScriptZh { get; set; }

    [Display(Name = "TTS tiếng Nhật")]
    public string? TtsScriptJa { get; set; }

    [Display(Name = "TTS tiếng Đức")]
    public string? TtsScriptDe { get; set; }

    [StringLength(500)]
    [Display(Name = "Audio tiếng Việt")]
    public string? AudioFileVi { get; set; }

    [StringLength(500)]
    [Display(Name = "Audio tiếng Anh")]
    public string? AudioFileEn { get; set; }

    [StringLength(500)]
    [Display(Name = "Audio tiếng Nhật")]
    public string? AudioFileJa { get; set; }

    [Display(Name = "Tải audio VI")]
    public IFormFile? AudioFileViUpload { get; set; }

    [Display(Name = "Tải audio EN")]
    public IFormFile? AudioFileEnUpload { get; set; }

    [Display(Name = "Tải audio JA")]
    public IFormFile? AudioFileJaUpload { get; set; }

    [StringLength(100)]
    [Display(Name = "Mã QR mặc định")]
    public string? QrCode { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;
}
