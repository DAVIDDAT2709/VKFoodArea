using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.ViewModels;

public class PoiIndexViewModel
{
    public string Query { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public List<Poi> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }
    public int ApprovedCount { get; set; }
}
