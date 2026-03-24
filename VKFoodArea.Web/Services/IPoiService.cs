using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;
using VKFoodArea.Web.Dtos;

namespace VKFoodArea.Web.Services;

public interface IPoiService
{
    Task<List<Poi>> GetAllAsync();
    Task<PoiFormViewModel?> GetEditFormAsync(int id);
    Task<Poi?> GetDeleteModelAsync(int id);
    Task CreateAsync(PoiFormViewModel vm);
    Task<bool> UpdateAsync(int id, PoiFormViewModel vm);
    Task<bool> DeleteAsync(int id);

    Task<List<PoiDto>> GetActiveForApiAsync();
    Task<PoiDto?> GetByIdForApiAsync(int id);
    Task<PoiDto?> GetByQrCodeForApiAsync(string qrCode);

    Task<string?> ValidateDefaultQrCodeAsync(int? currentPoiId, string qrCode);
}