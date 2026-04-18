namespace VKFoodArea.Web.ViewModels;

public class HomeDashboardViewModel
{
    public bool IsRestaurantOwnerDashboard { get; set; }
    public int PoiCount { get; set; }
    public int ActivePoiCount { get; set; }
    public int PendingPoiCount { get; set; }
    public int RejectedPoiCount { get; set; }
    public int ActiveQrCount { get; set; }
    public int NarrationHistoryCount { get; set; }
    public int TodayNarrationCount { get; set; }
    public int ConfiguredLanguageCount { get; set; }
    public int GpsNarrationCount { get; set; }
    public int QrNarrationCount { get; set; }
    public int ManualNarrationCount { get; set; }
    public int TourNarrationCount { get; set; }
    public int ActiveTourCount { get; set; }
    public double AverageListenSeconds { get; set; }
    public int AverageListenSampleCount { get; set; }
    public int ActiveDeviceCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int DeviceTimeoutSeconds { get; set; }
    public List<ActiveDeviceItemViewModel> ActiveDevices { get; set; } = new();
    public List<RecentNarrationItemViewModel> RecentNarrations { get; set; } = new();
    public List<DashboardBreakdownItemViewModel> LanguageBreakdown { get; set; } = new();
    public List<DashboardBreakdownItemViewModel> TriggerSourceBreakdown { get; set; } = new();
    public List<DashboardBreakdownItemViewModel> PlaybackModeBreakdown { get; set; } = new();
    public List<TopPoiPerformanceViewModel> TopPois { get; set; } = new();
}

public class RecentNarrationItemViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
}

public class DashboardBreakdownItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class TopPoiPerformanceViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LatestPlayedAt { get; set; }
}
