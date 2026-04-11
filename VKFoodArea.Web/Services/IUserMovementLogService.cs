using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IUserMovementLogService
{
    Task<MovementLogApiViewModel> CreateFromAppAsync(MovementLogCreateApiViewModel vm);
    Task<List<MovementLogApiViewModel>> GetRecentAsync(int top = 200);
}
