using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class PoiSyncService
{
    private readonly HttpClient _httpClient;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly AppDbContext _db;

    public PoiSyncService(
        HttpClient httpClient,
        ApiBaseUrlService apiBaseUrlService,
        AppDbContext db)
    {
        _httpClient = httpClient;
        _apiBaseUrlService = apiBaseUrlService;
        _db = db;
    }

    public async Task SyncPoisAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{_apiBaseUrlService.BaseUrl}api/pois";
            var remotePois = await _httpClient.GetFromJsonAsync<List<RemotePoiDto>>(url, ct);

            if (remotePois is null || remotePois.Count == 0)
                return;

            foreach (var dto in remotePois)
            {
                var local = await _db.Pois.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);

                if (local is null)
                {
                    local = new Poi();
                    _db.Pois.Add(local);
                }

                local.Id = dto.Id;
                local.Name = dto.Name;
                local.Address = dto.Address;
                local.Latitude = dto.Latitude;
                local.Longitude = dto.Longitude;
                local.RadiusMeters = dto.RadiusMeters;
                local.Description = dto.Description;
                local.TtsScriptVi = dto.TtsScriptVi;
                local.TtsScriptEn = dto.TtsScriptEn;
                local.TtsScriptZh = dto.TtsScriptZh;
                local.TtsScriptJa = dto.TtsScriptJa;
                local.TtsScriptDe = dto.TtsScriptDe;
                local.ImageUrl = dto.ImageUrl;
                local.QrCode = dto.QrCode;
                local.IsActive = dto.IsActive;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Web lỗi hoặc không mở thì app vẫn chạy local
        }
    }
}