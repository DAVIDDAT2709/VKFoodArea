using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IAnalyticsService
{
    Task<AdminMapAnalyticsViewModel> GetAdminMapAnalyticsAsync(
        int movementLogSampleSize = 2000,
        int maxDisplayedRoutes = 12,
        int maxHeatPoints = 300);
}
