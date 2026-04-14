using System.Net.Http.Json;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class TourCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;

    public TourCatalogService(HttpClient httpClient, ApiBaseUrlService apiBaseUrlService)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
    }

    public async Task<IReadOnlyList<Tour>> GetActiveToursAsync(CancellationToken ct = default)
    {
        var url = $"{_apiBaseUrlService.BaseUrl}api/tours";
        var remoteTours = await _httpClient.GetFromJsonAsync<List<RemoteTourDto>>(url, ct);

        if (remoteTours is null)
            return [];

        return remoteTours
            .Select(x => RemoteContentMapper.MapTour(x, _apiBaseUrlService))
            .ToList();
    }
}
