using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.ViewModels;

public class NarrationHistoryIndexViewModel
{
    public string? Query { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Language { get; set; }
    public string? Mode { get; set; }
    public string? Source { get; set; }
    public List<NarrationHistory> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PagedListViewModel<NarrationHistory>.DefaultPageSize;
    public int TotalItems { get; set; }
    public int TodayCount { get; set; }
    public int GpsCount { get; set; }
    public int QrCount { get; set; }
    public int ManualCount { get; set; }
    public int TourCount { get; set; }
    public List<TopPoiPerformanceViewModel> TopPois { get; set; } = new();
}
