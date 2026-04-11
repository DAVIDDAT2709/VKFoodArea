namespace VKFoodArea.Models;

public class AppUserSyncDto
{
    public string UserKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string NarrationLanguage { get; set; } = "vi";
    public string NarrationPlaybackMode { get; set; } = "TTS";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
}

public class AppUserStatusDto
{
    public string UserKey { get; set; } = string.Empty;
    public bool IsKnown { get; set; }
    public bool IsActive { get; set; } = true;
}
