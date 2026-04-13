using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface ITourService
{
    Task<List<Tour>> GetAllAsync();
    Task<TourFormViewModel> BuildCreateFormAsync();
    Task<TourFormViewModel?> GetEditFormAsync(int id);
    Task<Tour?> GetDeleteModelAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(TourFormViewModel vm);
    Task<(bool Success, string? Error)> UpdateAsync(int id, TourFormViewModel vm);
    Task<bool> DeleteAsync(int id);
    Task<List<TourDto>> GetActiveForApiAsync();
    Task<TourDto?> GetByIdForApiAsync(int id);
}
