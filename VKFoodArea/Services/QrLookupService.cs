using System.Net;
using System.Net.Http.Json;
using VKFoodArea.Helpers;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class QrLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public QrLookupService(HttpClient httpClient, ApiBaseUrlService apiBaseUrlService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
    }

    public async Task<QrResolveResult?> ResolveAsync(string qrCode, CancellationToken ct = default)
    {
        var normalized = QrCodePayload.Normalize(qrCode);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var url = $"{_apiBaseUrlService.BaseUrl}api/resolve-qr?code={Uri.EscapeDataString(normalized)}";

        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<RemoteQrResolveDto>(cancellationToken: ct);
        if (dto is null)
            return null;

        return new QrResolveResult
        {
            TargetType = QrTargetTypes.Normalize(dto.TargetType),
            TargetId = dto.TargetId,
            MatchedCode = dto.MatchedCode,
            Source = dto.Source,
            Poi = dto.Poi is null ? null : MapPoi(dto.Poi),
            Tour = dto.Tour is null ? null : MapTour(dto.Tour)
        };
    }

    public async Task<Poi?> FindPoiFromWebByQrAsync(string qrCode, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(qrCode, ct);
        return resolved?.Poi;
    }

    private Tour MapTour(RemoteTourDto dto)
    {
        return new Tour
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            Stops = dto.Stops
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new TourStop
                {
                    Id = x.Id,
                    PoiId = x.PoiId,
                    DisplayOrder = x.DisplayOrder,
                    Note = x.Note,
                    Poi = MapPoi(x.Poi)
                })
                .ToList()
        };
    }

    private Poi MapPoi(RemotePoiDto dto)
    {
        return new Poi
        {
            Id = dto.Id,
            Name = dto.Name,
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            ImageUrl = _apiBaseUrlService.ResolveImageUrl(dto.ImageUrl),
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = dto.RadiusMeters,
            Priority = dto.Priority,
            QrCode = dto.QrCode,
            IsActive = dto.IsActive,
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
}
