namespace VKFoodArea.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string NarrationLanguage { get; set; } = "vi";
    public string NarrationPlaybackMode { get; set; } = "TTS";
    public string Role { get; set; } = "User"; // Admin, User
    public bool IsActive { get; set; } = true;
}
