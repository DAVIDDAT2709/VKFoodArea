using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class AdminUserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tài khoản.")]
    [StringLength(80)]
    [Display(Name = "Tài khoản")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(160)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Vai trò")]
    public string Role { get; set; } = "Admin";

    [DataType(DataType.Password)]
    [StringLength(120, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string? Password { get; set; }

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;
}

public sealed record AdminUserSaveResult(bool Success, string? Error = null)
{
    public static AdminUserSaveResult Ok() => new(true);

    public static AdminUserSaveResult Fail(string error) => new(false, error);
}
