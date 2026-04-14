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
            Poi = dto.Poi is null ? null : RemoteContentMapper.MapPoi(dto.Poi, _apiBaseUrlService),
            Tour = dto.Tour is null ? null : RemoteContentMapper.MapTour(dto.Tour, _apiBaseUrlService)
        };
    }

    public async Task<Poi?> FindPoiFromWebByQrAsync(string qrCode, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(qrCode, ct);
        return resolved?.Poi;
    }
}
