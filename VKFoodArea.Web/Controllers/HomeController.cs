using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers;

public class HomeController : Controller
{
    private readonly IHomeService _homeService;

    public HomeController(IHomeService homeService)
    {
        _homeService = homeService;
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