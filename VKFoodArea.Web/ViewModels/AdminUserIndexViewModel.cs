using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.ViewModels;

public class AdminUserIndexViewModel
{
    public PagedListViewModel<AdminUser> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
}
