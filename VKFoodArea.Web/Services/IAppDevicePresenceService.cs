using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IAppDevicePresenceService
{
    Task UpsertHeartbeatAsync(AppDeviceHeartbeatViewModel vm);
    Task<ActiveDeviceSummaryViewModel> GetSummaryAsync();
}