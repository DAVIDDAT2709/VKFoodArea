using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize]
public class MapController : Controller
{
    private readonly IPoiService _poiService;

    public MapController(IPoiService poiService)
    {
        _poiService = poiService;
    }

    public async Task<IActionResult> Index()
    {
        var pois = await _poiService.GetAllAsync();
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
                    Priority = x.Priority
                })
                .ToList()
        };

        return View(vm);
    }
}
