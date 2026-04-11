using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Controllers;

[Authorize]
public class AppUsersController : Controller
{
    private readonly IAppUserAccountService _appUserAccountService;

    public AppUsersController(IAppUserAccountService appUserAccountService)
    {
        _appUserAccountService = appUserAccountService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _appUserAccountService.GetAllAsync();
        return View(users);
    }

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _appUserAccountService.GetDetailsAsync(id);
        if (vm is null)
            return NotFound();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var updated = await _appUserAccountService.SetActiveAsync(id, isActive);
        TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated
            ? isActive ? "Da mo khoa user App." : "Da khoa user App."
            : "Khong tim thay user App.";

        return RedirectToAction(nameof(Index));
    }
}
