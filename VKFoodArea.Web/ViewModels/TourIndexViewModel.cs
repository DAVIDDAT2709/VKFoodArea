using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.ViewModels;

public class TourIndexViewModel
{
    public PagedListViewModel<Tour> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
}
