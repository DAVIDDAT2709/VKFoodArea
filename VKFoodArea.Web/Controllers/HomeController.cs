using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOrRestaurantOwner)]
public class HomeController : Controller
{
    private readonly IHomeService _homeService;
    private readonly IAppDevicePresenceService _appDevicePresenceService;

    public HomeController(
        IHomeService homeService,
        IAppDevicePresenceService appDevicePresenceService)
    {
        _homeService = homeService;
        _appDevicePresenceService = appDevicePresenceService;
    }

    [HttpGet]
    public async Task<IActionResult> ActiveDevices()
    {
        var vm = await _appDevicePresenceService.GetSummaryAsync();
        return Json(vm);
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _homeService.GetDashboardAsync();
        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
