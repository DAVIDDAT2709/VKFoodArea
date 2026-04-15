using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class AppDeviceSession
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string DeviceKey { get; set; } = string.Empty;

    [StringLength(80)]
    public string UserKey { get; set; } = string.Empty;

    [StringLength(80)]
    public string Username { get; set; } = string.Empty;

    [StringLength(160)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(40)]
    public string Platform { get; set; } = string.Empty;

    [StringLength(120)]
    public string DeviceName { get; set; } = string.Empty;

    [StringLength(40)]
    public string AppVersion { get; set; } = string.Empty;

    public bool IsOnline { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeatAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastOfflineAt { get; set; }
}