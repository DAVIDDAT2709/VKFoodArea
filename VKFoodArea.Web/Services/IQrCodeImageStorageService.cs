using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public interface IQrCodeImageStorageService
{
    string? Validate(IFormFile? imageFile);
    Task<string> SaveAsync(IFormFile imageFile, string qrCode, CancellationToken ct = default);
}
