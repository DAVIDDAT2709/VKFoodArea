using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class HomeService : IHomeService
{
    private readonly AppDbContext _context;

    public HomeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HomeDashboardViewModel> GetDashboardAsync()
    {
        var activePois = await _context.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        var narrationHistoryCount = await _context.NarrationHistories.CountAsync();
        var today = DateTime.Today;

        return new HomeDashboardViewModel
        {
            PoiCount = await _context.Pois.CountAsync(),
            ActivePoiCount = activePois.Count,
            DefaultQrCount = activePois.Count(x => !string.IsNullOrWhiteSpace(x.QrCode)),
            ActiveQrCount = await _context.QrCodeItems.CountAsync(x => x.IsActive),
            NarrationHistoryCount = narrationHistoryCount,
            TodayNarrationCount = await _context.NarrationHistories.CountAsync(x => x.PlayedAt >= today),
            ConfiguredLanguageCount = CountConfiguredLanguages(activePois),
            RecentNarrations = await _context.NarrationHistories
                .AsNoTracking()
                .OrderByDescending(x => x.PlayedAt)
                .Take(6)
                .Select(x => new RecentNarrationItemViewModel
                {
                    PoiName = x.PoiName,
                    Language = x.Language,
                    TriggerSource = x.TriggerSource,
                    PlayedAt = x.PlayedAt
                })
                .ToListAsync(),
            TriggerBreakdown = await GetBreakdownAsync(
                x => x.TriggerSource,
                narrationHistoryCount,
                value => value switch
                {
                    "gps" => "GPS",
                    "qr" => "QR",
                    "manual" => "Thủ công",
                    _ => value.ToUpperInvariant()
                }),
            LanguageBreakdown = await GetBreakdownAsync(
                x => x.Language,
                narrationHistoryCount,
                value => value.ToUpperInvariant()),
            TopPois = await _context.NarrationHistories
                .AsNoTracking()
                .GroupBy(x => x.PoiName)
                .Select(x => new TopPoiPerformanceViewModel
                {
                    PoiName = x.Key,
                    Count = x.Count(),
                    LastPlayedAt = x.Max(item => item.PlayedAt)
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PoiName)
                .Take(5)
                .ToListAsync()
        };
    }

    private async Task<List<DashboardBreakdownItemViewModel>> GetBreakdownAsync(
        Expression<Func<NarrationHistory, string>> selector,
        int total,
        Func<string, string> labelMapper)
    {
        var items = await _context.NarrationHistories
            .AsNoTracking()
            .GroupBy(selector)
            .Select(x => new
            {
                Key = x.Key,
                Count = x.Count(),
            })
            .ToListAsync();

        return items
            .Select(x => new DashboardBreakdownItemViewModel
            {
                Label = labelMapper(x.Key),
                Count = x.Count,
                Percent = total == 0 ? 0 : Math.Round((double)x.Count * 100 / total, 1)
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .ToList();
    }

    private static int CountConfiguredLanguages(IEnumerable<Poi> pois)
    {
        var count = 0;

        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptVi))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptEn))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptZh))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptJa))) count++;
        if (pois.Any(x => !string.IsNullOrWhiteSpace(x.TtsScriptDe))) count++;

        return count;
    }
}
