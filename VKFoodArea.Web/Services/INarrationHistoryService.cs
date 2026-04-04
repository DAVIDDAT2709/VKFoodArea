using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface INarrationHistoryService
{
    Task<List<NarrationHistory>> GetAllAsync(string? source);
    Task<NarrationHistoryApiViewModel?> CreateFromAppAsync(NarrationHistoryCreateApiViewModel vm);
    Task<NarrationHistoryApiViewModel?> GetByIdForApiAsync(int id);
}
