using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class AppUserAccount
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string UserKey { get; set; } = string.Empty;

    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(10)]
    public string NarrationLanguage { get; set; } = "vi";

    [StringLength(20)]
    public string NarrationPlaybackMode { get; set; } = "TTS";

    [StringLength(40)]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
