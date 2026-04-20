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

    public async Task<IActionResult> Index(string? query, string? role, string? status, int page = 1)
    {
        var vm = await _adminUserService.GetIndexAsync(query, role, status, page);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var ok = await _adminUserService.ResetPasswordAsync(id, User.Identity?.Name);
        TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
            ? "Đã reset mật khẩu về 123456."
            : "Không thể reset mật khẩu.";

        return RedirectToAction(nameof(Index));
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

        var result = await _adminUserService.CreateAsync(vm, User.Identity?.Name);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể tạo tài khoản.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã tạo tài khoản mới.";
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

        var result = await _adminUserService.UpdateAsync(id, vm, User.Identity?.Name);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể cập nhật tài khoản.");
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã cập nhật tài khoản.";
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
            TempData["ErrorMessage"] = result.Error ?? "Không thể xóa tài khoản.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Đã xóa tài khoản.";
        return RedirectToAction(nameof(Index));
    }
}
