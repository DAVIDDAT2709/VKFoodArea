using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public interface IPoiImageStorageService
{
    string? Validate(IFormFile? imageFile);
    Task<string> SaveAsync(IFormFile imageFile, string poiName, CancellationToken ct = default);
}
