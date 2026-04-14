using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface INarrationHistoryService
{
    Task<NarrationHistoryIndexViewModel> GetIndexAsync(
        string? query,
        DateTime? fromDate,
        DateTime? toDate,
        string? language,
        string? mode,
        string? source);
    Task<List<NarrationHistory>> GetAllAsync(string? source);
    Task<List<NarrationHistoryApiViewModel>> GetRecentForApiAsync(string? source, string? userKey, int top = 100);
    Task<NarrationHistoryApiViewModel?> CreateFromAppAsync(NarrationHistoryCreateApiViewModel vm);
    Task<NarrationHistoryApiViewModel?> GetByIdForApiAsync(int id);
    Task<int> ClearForApiAsync(string? userKey, string? source);
}
