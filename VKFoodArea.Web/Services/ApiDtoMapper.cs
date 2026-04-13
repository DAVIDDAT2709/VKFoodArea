using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.Services;

internal static class ApiDtoMapper
{
    public static PoiDto ToPoiDto(Poi poi, string matchedQrCode, string qrSource)
    {
        return new PoiDto
        {
            Id = poi.Id,
            Name = poi.Name,
            Address = poi.Address,
            PhoneNumber = poi.PhoneNumber,
            ImageUrl = poi.ImageUrl,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            RadiusMeters = poi.RadiusMeters,
            Priority = poi.Priority,
            QrCode = poi.QrCode,
            IsActive = poi.IsActive,
            TtsScriptVi = GetTranslationScript(poi, "vi", poi.TtsScriptVi),
            TtsScriptEn = GetTranslationScript(poi, "en", poi.TtsScriptEn),
            TtsScriptZh = GetTranslationScript(poi, "zh", poi.TtsScriptZh),
            TtsScriptJa = GetTranslationScript(poi, "ja", poi.TtsScriptJa),
            TtsScriptDe = GetTranslationScript(poi, "de", poi.TtsScriptDe),
            AudioFileVi = GetAudioFile(poi, "vi", poi.AudioFileVi),
            AudioFileEn = GetAudioFile(poi, "en", poi.AudioFileEn),
            AudioFileJa = GetAudioFile(poi, "ja", poi.AudioFileJa),
            MatchedQrCode = matchedQrCode,
            QrSource = qrSource
        };
    }

    public static TourDto ToTourDto(Tour tour)
    {
        return new TourDto
        {
            Id = tour.Id,
            Name = tour.Name,
            Description = tour.Description,
            IsActive = tour.IsActive,
            Stops = tour.Stops
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new TourStopDto
                {
                    Id = x.Id,
                    PoiId = x.PoiId,
                    DisplayOrder = x.DisplayOrder,
                    Note = x.Note,
                    Poi = ToPoiDto(
                        x.Poi ?? new Poi { Id = x.PoiId, Name = $"POI #{x.PoiId}" },
                        x.Poi?.QrCode ?? string.Empty,
                        "tour-stop")
                })
                .ToList()
        };
    }

    private static string GetTranslationScript(Poi poi, string language, string fallback)
        => poi.Translations
               .FirstOrDefault(x => x.Language == language)?
               .Script
               .Trim()
           ?? fallback;

    private static string GetAudioFile(Poi poi, string language, string fallback)
        => poi.AudioAssets
               .FirstOrDefault(x => x.Language == language && x.IsActive)?
               .FileUrl
               .Trim()
           ?? fallback;
}
