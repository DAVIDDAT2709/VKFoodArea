using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.ViewModels;

public class AppDeviceHeartbeatViewModel
{
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
}

public class ActiveDeviceSummaryViewModel
{
    public int ActiveDeviceCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int TimeoutSeconds { get; set; }
    public List<ActiveDeviceItemViewModel> Devices { get; set; } = new();
}

public class ActiveDeviceItemViewModel
{
    public string DeviceKey { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DateTime LastHeartbeatAt { get; set; }
    public string LastHeartbeatDisplay { get; set; } = string.Empty;
}
