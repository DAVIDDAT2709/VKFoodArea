using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.Services;

public class QrResolveService : IQrResolveService
{
    private readonly AppDbContext _context;

    public QrResolveService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ResolveQrResponseDto?> ResolveAsync(string qrCode)
    {
        var normalized = QrCodeHelper.Normalize(qrCode);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var qrItem = await _context.QrCodeItems
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.Code) &&
                x.Code.ToLower() == normalized)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (qrItem is not null)
        {
            var resolvedFromQrItem = await ResolveQrItemTargetAsync(qrItem);
            if (resolvedFromQrItem is not null)
                return resolvedFromQrItem;
        }

        var poi = await BuildPoiQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == normalized);

        if (poi is null)
            return null;

        return new ResolveQrResponseDto
        {
            TargetType = QrTargetTypes.Poi,
            TargetId = poi.Id,
            MatchedCode = poi.QrCode,
            Source = "poi-default",
            Poi = ApiDtoMapper.ToPoiDto(poi, poi.QrCode, "poi-default")
        };
    }

    private async Task<ResolveQrResponseDto?> ResolveQrItemTargetAsync(QrCodeItem qrItem)
    {
        var targetType = QrTargetTypes.Normalize(qrItem.TargetType);

        if (targetType == QrTargetTypes.Tour)
        {
            var tour = await BuildTourQuery()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == qrItem.TargetId && x.IsActive);

            if (tour is null)
                return null;

            return new ResolveQrResponseDto
            {
                TargetType = QrTargetTypes.Tour,
                TargetId = tour.Id,
                MatchedCode = qrItem.Code,
                Source = "qr-item",
                Tour = ApiDtoMapper.ToTourDto(tour)
            };
        }

        var poi = await BuildPoiQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == qrItem.TargetId && x.IsActive);

        if (poi is null)
            return null;

        return new ResolveQrResponseDto
        {
            TargetType = QrTargetTypes.Poi,
            TargetId = poi.Id,
            MatchedCode = qrItem.Code,
            Source = "qr-item",
            Poi = ApiDtoMapper.ToPoiDto(poi, qrItem.Code, "qr-item")
        };
    }

    private IQueryable<Poi> BuildPoiQuery()
        => _context.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets);

    private IQueryable<Tour> BuildTourQuery()
        => _context.Tours
            .Include(x => x.Stops.OrderBy(stop => stop.DisplayOrder))
            .ThenInclude(x => x.Poi)
            .ThenInclude(x => x!.Translations)
            .Include(x => x.Stops.OrderBy(stop => stop.DisplayOrder))
            .ThenInclude(x => x.Poi)
            .ThenInclude(x => x!.AudioAssets);
}
