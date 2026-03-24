using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IQrCodeItemService
{
    Task<List<QrCodeItem>> GetAllAsync();
    Task<QrCodeItemFormViewModel> BuildCreateFormAsync();
    Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm);
}