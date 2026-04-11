using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public interface IPoiAudioStorageService
{
    string? Validate(IFormFile? audioFile);
    Task<string> SaveAsync(IFormFile audioFile, string poiName, string language, CancellationToken ct = default);
}
