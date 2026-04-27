using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
public class AppUserAccountsController : Controller
{
    private readonly IAppUserAccountService _appUserAccountService;

    public AppUserAccountsController(IAppUserAccountService appUserAccountService)
    {
        _appUserAccountService = appUserAccountService;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _appUserAccountService.GetAllAsync();
        return View(items);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var details = await _appUserAccountService.GetDetailsAsync(id);
        if (details is null)
            return NotFound();

        return View(ToAccessForm(details.User));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppUserAccountAccessFormViewModel vm)
    {
        if (id != vm.Id)
            return NotFound();

        vm.Role = NormalizeRoleForPost(vm.Role);
        if (!ModelState.IsValid)
            return View(vm);

        var updated = await _appUserAccountService.UpdateAccessAsync(
            id,
            vm.Role,
            vm.IsActive,
            User.Identity?.Name);

        if (!updated)
            return NotFound();

        TempData["SuccessMessage"] = "Da cap nhat quyen tai khoan app.";
        return RedirectToAction(nameof(Index));
    }

    private static AppUserAccountAccessFormViewModel ToAccessForm(AppUserAccountListItemViewModel user)
        => new()
        {
            Id = user.Id,
            UserKey = user.UserKey,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            ListenCount = user.ListenCount,
            CreatedAt = user.CreatedAt,
            LastSeenAt = user.LastSeenAt,
            LastSyncedAt = user.LastSyncedAt,
            Role = AppUserRoleNames.Normalize(user.Role),
            IsActive = user.IsActive
        };

    private static string NormalizeRoleForPost(string? role)
    {
        var normalized = AppUserRoleNames.Normalize(role);
        return normalized == AppUserRoleNames.Guest
            ? AppUserRoleNames.User
            : normalized;
    }
}
