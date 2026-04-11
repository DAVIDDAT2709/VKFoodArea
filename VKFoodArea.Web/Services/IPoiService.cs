using Microsoft.AspNetCore.Http;
using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IPoiService
{
    Task<List<Poi>> GetAllAsync();
    Task<PoiFormViewModel?> GetEditFormAsync(int id);
    Task<Poi?> GetDeleteModelAsync(int id);
    Task<int> CreateAsync(PoiFormViewModel vm);
    Task<bool> UpdateAsync(int id, PoiFormViewModel vm);
    Task<bool> DeleteAsync(int id);

    Task<List<PoiDto>> GetActiveForApiAsync();
    Task<PoiDto?> GetByIdForApiAsync(int id);
    Task<PoiDto?> GetByQrCodeForApiAsync(string qrCode);

    Task<string?> ValidateDefaultQrCodeAsync(int? currentPoiId, string? qrCode);
    string? ValidateImageFile(IFormFile? imageFile);
    string? ValidateAudioFile(IFormFile? audioFile);
}
