using VKFoodArea.Web.Dtos;

namespace VKFoodArea.Web.Services;

public interface IQrResolveService
{
    Task<ResolveQrResponseDto?> ResolveAsync(string qrCode);
}
