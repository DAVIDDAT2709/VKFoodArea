using System.Net;
using System.Net.Http.Json;
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

    public async Task<Poi?> FindPoiFromWebByQrAsync(string qrCode, CancellationToken ct = default)
    {
        var normalized = (qrCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var url = $"{_apiBaseUrlService.BaseUrl}api/pois/by-qr?code={Uri.EscapeDataString(normalized)}";

        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<RemotePoiDto>(cancellationToken: ct);
        if (dto is null)
            return null;

        return new Poi
        {
            Id = dto.Id,
            Name = dto.Name,
            Address = dto.Address,
            PhoneNumber = dto.PhoneNumber,
            ImageUrl = dto.ImageUrl,
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = dto.RadiusMeters,
            QrCode = dto.QrCode,
            IsActive = dto.IsActive,
            TtsScriptVi = dto.TtsScriptVi,
            TtsScriptEn = dto.TtsScriptEn,
            TtsScriptZh = dto.TtsScriptZh,
            TtsScriptJa = dto.TtsScriptJa,
            TtsScriptDe = dto.TtsScriptDe
        };
    }
}
