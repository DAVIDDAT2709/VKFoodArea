using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers;

public class NarrationHistoriesController : Controller
{
    private readonly INarrationHistoryService _narrationHistoryService;

    public NarrationHistoriesController(INarrationHistoryService narrationHistoryService)
    {
        _narrationHistoryService = narrationHistoryService;
    }

    public async Task<IActionResult> Index(string? source)
    {
        var data = await _narrationHistoryService.GetAllAsync(source);

        ViewBag.CurrentSource = source;
        return View(data);
    }
}