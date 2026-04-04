using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IQrCodeItemService
{
    Task<List<QrCodeItem>> GetAllAsync();
    Task<QrCodeItemFormViewModel> BuildCreateFormAsync();
    Task<QrCodeItemFormViewModel?> GetEditFormAsync(int id);
    Task<QrCodeItem?> GetDeleteModelAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm);
    Task<(bool Success, string? Error)> UpdateAsync(int id, QrCodeItemFormViewModel vm);
    Task<bool> DeleteAsync(int id);
}
