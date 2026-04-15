namespace VKFoodArea.Models;

public class AppDeviceHeartbeatDto
{
    public string DeviceKey { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = true;
}