using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class NarrationHistoryService : INarrationHistoryService
{
    private readonly AppDbContext _context;

    public NarrationHistoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<NarrationHistory>> GetAllAsync(string? source)
    {
        var normalizedSource = NormalizeTriggerSource(source);

        var query = _context.NarrationHistories
            .AsNoTracking()
            .OrderByDescending(x => x.PlayedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSource))
        {
            if (normalizedSource == "gps")
            {
                query = query.Where(x =>
                    x.TriggerSource == "gps" ||
                    x.TriggerSource == "auto");
            }
            else
            {
                query = query.Where(x => x.TriggerSource == normalizedSource);
            }
        }

        return await query.ToListAsync();
    }

    public async Task<NarrationHistoryApiViewModel?> CreateFromAppAsync(NarrationHistoryCreateApiViewModel vm)
    {
        var language = (vm.Language ?? "vi").Trim().ToLowerInvariant();
        var triggerSource = NormalizeTriggerSource(vm.TriggerSource);
        var mode = (vm.Mode ?? "tts").Trim().ToLowerInvariant();
        var poi = await ResolvePoiAsync(vm);

        if (poi is null)
            return null;

        var entity = new NarrationHistory
        {
            PoiId = poi.Id,
            PoiName = poi.Name,
            Language = language,
            TriggerSource = triggerSource,
            Mode = mode,
            PlayedAt = vm.PlayedAt ?? DateTime.UtcNow
        };

        _context.NarrationHistories.Add(entity);
        await _context.SaveChangesAsync();

        return new NarrationHistoryApiViewModel
        {
            Id = entity.Id,
            PoiId = entity.PoiId,
            PoiName = entity.PoiName,
            Language = entity.Language,
            TriggerSource = entity.TriggerSource,
            Mode = entity.Mode,
            PlayedAt = entity.PlayedAt
        };
    }

    public async Task<NarrationHistoryApiViewModel?> GetByIdForApiAsync(int id)
    {
        return await _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new NarrationHistoryApiViewModel
            {
                Id = x.Id,
                PoiId = x.PoiId,
                PoiName = x.PoiName,
                Language = x.Language,
                TriggerSource = x.TriggerSource,
                Mode = x.Mode,
                PlayedAt = x.PlayedAt
            })
            .FirstOrDefaultAsync();
    }

    private async Task<Poi?> ResolvePoiAsync(NarrationHistoryCreateApiViewModel vm)
{
    // Id trong app local có thể khác Id thật của web sau khi sync,
    // nên phải ưu tiên nhận diện bằng QR hoặc tên quán trước.
    var normalizedQrCode = QrCodeHelper.Normalize(vm.QrCode);
    if (!string.IsNullOrWhiteSpace(normalizedQrCode))
    {
        var poiByQr = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == normalizedQrCode);

        if (poiByQr is not null)
            return poiByQr;
    }

    var normalizedPoiName = (vm.PoiName ?? string.Empty).Trim();
    if (!string.IsNullOrWhiteSpace(normalizedPoiName))
    {
        var poiByName = await _context.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .FirstOrDefaultAsync(x => x.Name == normalizedPoiName);

        if (poiByName is not null)
            return poiByName;
    }

    if (vm.PoiId > 0)
    {
        var poiById = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vm.PoiId && x.IsActive);

        if (poiById is not null)
            return poiById;
    }

    return null;
}

    private static string NormalizeTriggerSource(string? source)
    {
        var normalized = (source ?? "manual").Trim().ToLowerInvariant();

        return normalized switch
        {
            "auto" => "gps",
            "gps" => "gps",
            "qr" => "qr",
            _ => "manual"
        };
    }
}
