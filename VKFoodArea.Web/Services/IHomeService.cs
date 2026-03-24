using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IHomeService
{
    Task<HomeDashboardViewModel> GetDashboardAsync();
}