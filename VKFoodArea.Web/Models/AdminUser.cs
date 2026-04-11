using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class AdminUser
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(240)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Role { get; set; } = "Admin";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }
}
