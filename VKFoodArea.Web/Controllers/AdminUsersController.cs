using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
public class AdminUsersController : Controller
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var users = await _adminUserService.GetAllAsync();
        var vm = new AdminUserIndexViewModel
        {
            Items = PagedListViewModel<AdminUser>.Create(users, page),
            TotalCount = users.Count,
            ActiveCount = users.Count(x => x.IsActive)
        };

        return View(vm);
    }

    public IActionResult Create()
    {
        return View(new AdminUserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserFormViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        var result = await _adminUserService.CreateAsync(vm);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể tạo admin.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã tạo admin mới.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _adminUserService.GetEditFormAsync(id);
        if (vm is null)
            return NotFound();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminUserFormViewModel vm)
    {
        if (id != vm.Id)
            return NotFound();

        if (!ModelState.IsValid)
            return View(vm);

        var result = await _adminUserService.UpdateAsync(id, vm);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể cập nhật admin.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã cập nhật admin.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var user = await _adminUserService.GetDeleteModelAsync(id);
        if (user is null)
            return NotFound();

        return View(user);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _adminUserService.DeleteAsync(id, User.Identity?.Name);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Error ?? "Không thể xóa admin.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Đã xóa admin.";
        return RedirectToAction(nameof(Index));
    }
}
