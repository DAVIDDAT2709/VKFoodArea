using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IAppUserAccountService
{
    Task<List<AppUserAccountListItemViewModel>> GetAllAsync();
    Task<AppUserAccountDetailsViewModel?> GetDetailsAsync(int id);
    Task<AppUserAccountListItemViewModel> SyncFromAppAsync(AppUserAccountSyncViewModel vm);
    Task<AppUserAccountStatusViewModel> GetStatusAsync(string? userKey);
    Task<bool> UpdateAccessAsync(int id, string role, bool isActive, string? actorUsername);
}
