using System.ComponentModel.DataAnnotations;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.ViewModels;

public class AppUserAccountSyncViewModel
{
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
}

public class AppUserAccountStatusViewModel
{
    public string UserKey { get; set; } = string.Empty;
    public bool IsKnown { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AppUserAccountListItemViewModel
{
    public int Id { get; set; }
    public string UserKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string NarrationLanguage { get; set; } = "vi";
    public string NarrationPlaybackMode { get; set; } = "TTS";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public int ListenCount { get; set; }
    public DateTime? LatestPlayedAt { get; set; }
}

public class AppUserAccountDetailsViewModel
{
    public AppUserAccountListItemViewModel User { get; set; } = new();
    public List<NarrationHistory> RecentNarrations { get; set; } = new();
}
