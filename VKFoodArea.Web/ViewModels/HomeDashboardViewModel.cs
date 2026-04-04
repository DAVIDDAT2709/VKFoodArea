namespace VKFoodArea.Web.ViewModels;

public class HomeDashboardViewModel
{
    public int PoiCount { get; set; }
    public int ActivePoiCount { get; set; }
    public int DefaultQrCount { get; set; }
    public int ActiveQrCount { get; set; }
    public int NarrationHistoryCount { get; set; }
    public int TodayNarrationCount { get; set; }
    public int ConfiguredLanguageCount { get; set; }
    public List<RecentNarrationItemViewModel> RecentNarrations { get; set; } = new();
    public List<DashboardBreakdownItemViewModel> TriggerBreakdown { get; set; } = new();
    public List<DashboardBreakdownItemViewModel> LanguageBreakdown { get; set; } = new();
    public List<TopPoiPerformanceViewModel> TopPois { get; set; } = new();
}

public class RecentNarrationItemViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
}

public class DashboardBreakdownItemViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
}

public class TopPoiPerformanceViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime? LastPlayedAt { get; set; }
}
