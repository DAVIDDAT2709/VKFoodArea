using VKFoodArea.Models;

namespace VKFoodArea.Services;

internal static class RemoteContentMapper
{
    public static Tour MapTour(RemoteTourDto dto, ApiBaseUrlService apiBaseUrlService)
    {
        return new Tour
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            TtsScriptVi = dto.TtsScriptVi,
            TtsScriptEn = dto.TtsScriptEn,
            TtsScriptZh = dto.TtsScriptZh,
            TtsScriptJa = dto.TtsScriptJa,
            TtsScriptDe = dto.TtsScriptDe,
            IsActive = dto.IsActive,
            Stops = dto.Stops
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new TourStop
                {
                    Id = x.Id,
                    PoiId = x.PoiId,
                    DisplayOrder = x.DisplayOrder,
                    Note = x.Note,
                    Poi = MapPoi(x.Poi, apiBaseUrlService)
                })
                .ToList()
        };
    }

    public static Poi MapPoi(RemotePoiDto dto, ApiBaseUrlService apiBaseUrlService)
    {
        return new Poi
        {
            Id = dto.Id,
            Name = dto.Name,
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            ImageUrl = apiBaseUrlService.ResolveImageUrl(dto.ImageUrl),
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = dto.RadiusMeters,
            Priority = dto.Priority,
            QrCode = dto.QrCode,
            IsActive = dto.IsActive,
            MapUrl = CreateMapUrl(dto.Latitude, dto.Longitude),
            TtsScriptVi = dto.TtsScriptVi,
            TtsScriptEn = dto.TtsScriptEn,
            TtsScriptZh = dto.TtsScriptZh,
            TtsScriptJa = dto.TtsScriptJa,
            TtsScriptDe = dto.TtsScriptDe,
            AudioFileVi = dto.AudioFileVi,
            AudioFileEn = dto.AudioFileEn,
            AudioFileJa = dto.AudioFileJa
        };
    }

    private static string CreateMapUrl(double latitude, double longitude)
        => $"https://maps.google.com/?q={latitude},{longitude}";
}
