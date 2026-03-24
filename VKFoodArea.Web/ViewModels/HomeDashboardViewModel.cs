namespace VKFoodArea.Web.ViewModels;

public class HomeDashboardViewModel
{
    public int PoiCount { get; set; }
    public int ActiveQrCount { get; set; }
    public int NarrationHistoryCount { get; set; }
    public List<RecentNarrationItemViewModel> RecentNarrations { get; set; } = new();
}

public class RecentNarrationItemViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; }
}
