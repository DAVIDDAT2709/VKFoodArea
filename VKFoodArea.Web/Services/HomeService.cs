using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
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
        return new HomeDashboardViewModel
        {
            PoiCount = await _context.Pois.CountAsync(),
            ActiveQrCount = await _context.QrCodeItems.CountAsync(x => x.IsActive),
            NarrationHistoryCount = await _context.NarrationHistories.CountAsync(),
            RecentNarrations = await _context.NarrationHistories
                .AsNoTracking()
                .OrderByDescending(x => x.PlayedAt)
                .Take(5)
                .Select(x => new RecentNarrationItemViewModel
                {
                    PoiName = x.PoiName,
                    Language = x.Language,
                    TriggerSource = x.TriggerSource,
                    PlayedAt = x.PlayedAt
                })
                .ToListAsync()
        };
    }
}