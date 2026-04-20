using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class AdminUserFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Tài khoản")]
    [Required(ErrorMessage = "Vui lòng nhập tài khoản.")]
    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Display(Name = "Họ tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Vai trò")]
    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [StringLength(40)]
    public string Role { get; set; } = "Admin";

    [Display(Name = "Mật khẩu")]
    [DataType(DataType.Password)]
    [StringLength(120, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string? Password { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;

    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int OwnedPoiCount { get; set; }

    public bool ShowAdminInfo => IsEdit;
}

public sealed record AdminUserSaveResult(bool Success, string? Error = null)
{
    public static AdminUserSaveResult Ok() => new(true);

    public static AdminUserSaveResult Fail(string error) => new(false, error);
}