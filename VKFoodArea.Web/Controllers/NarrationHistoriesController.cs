using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOrRestaurantOwner)]
public class NarrationHistoriesController : Controller
{
    private readonly INarrationHistoryService _narrationHistoryService;

    public NarrationHistoriesController(INarrationHistoryService narrationHistoryService)
    {
        _narrationHistoryService = narrationHistoryService;
    }

    public async Task<IActionResult> Index(
        string? query,
        DateTime? fromDate,
        DateTime? toDate,
        string? language,
        string? mode,
        string? source)
    {
        var vm = await _narrationHistoryService.GetIndexAsync(
            query,
            fromDate,
            toDate,
            language,
            mode,
            source);

        return View(vm);
    }
}
