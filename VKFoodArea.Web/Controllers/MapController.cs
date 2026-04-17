using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
public class MapController : Controller
{
    private readonly IPoiService _poiService;
    private readonly IAnalyticsService _analyticsService;

    public MapController(IPoiService poiService, IAnalyticsService analyticsService)
    {
        _poiService = poiService;
        _analyticsService = analyticsService;
    }

    public async Task<IActionResult> Index()
    {
        var pois = await _poiService.GetAllAsync();
        var analytics = await _analyticsService.GetAdminMapAnalyticsAsync();
        var listenLookup = analytics.TopPois.ToDictionary(x => x.PoiId, x => x.Count);
        var topPoiId = analytics.TopPois.FirstOrDefault()?.PoiId;
        var vm = new AdminMapViewModel
        {
            ActivePois = pois
                .Where(x => x.IsActive)
                .Select(x => new AdminMapPoiViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Address = x.Address,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    RadiusMeters = x.RadiusMeters,
                    Priority = x.Priority,
                    ListenCount = listenLookup.GetValueOrDefault(x.Id),
                    IsTopListened = topPoiId == x.Id
                })
                .ToList(),
            Analytics = analytics
        };

        return View(vm);
    }
}
