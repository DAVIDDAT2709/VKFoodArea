using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IQrCodeItemService
{
    Task<List<QrCodeItemListItemViewModel>> GetAllAsync();
    Task<QrCodeItemFormViewModel> BuildCreateFormAsync();
    Task<QrCodeItemFormViewModel?> GetEditFormAsync(int id);
    Task<QrCodeItemDeleteViewModel?> GetDeleteModelAsync(int id);
    string? ValidateImageFile(Microsoft.AspNetCore.Http.IFormFile? imageFile);
    Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm);
    Task<(bool Success, string? Error)> UpdateAsync(int id, QrCodeItemFormViewModel vm);
    Task<bool> DeleteAsync(int id);
}
