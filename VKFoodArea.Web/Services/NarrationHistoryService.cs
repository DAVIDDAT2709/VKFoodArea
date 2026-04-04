using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
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
        var query = _context.NarrationHistories
            .AsNoTracking()
            .Include(x => x.Poi)
            .OrderByDescending(x => x.PlayedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(x => x.TriggerSource == source);
        }

        return await query.ToListAsync();
    }

    public async Task<NarrationHistoryApiViewModel?> CreateFromAppAsync(NarrationHistoryCreateApiViewModel vm)
    {
        var language = (vm.Language ?? "vi").Trim().ToLowerInvariant();
        var triggerSource = (vm.TriggerSource ?? "manual").Trim().ToLowerInvariant();
        var mode = (vm.Mode ?? "tts").Trim().ToLowerInvariant();

        var poi = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vm.PoiId && x.IsActive);

        if (poi is null)
            return null;

        var entity = new NarrationHistory
        {
            PoiId = vm.PoiId,
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
}
