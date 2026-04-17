using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
public class ToursController : Controller
{
    private readonly ITourService _tourService;

    public ToursController(ITourService tourService)
    {
        _tourService = tourService;
    }

    public async Task<IActionResult> Index()
    {
        var tours = await _tourService.GetAllAsync();
        return View(tours);
    }

    public async Task<IActionResult> Create()
    {
        var vm = await _tourService.BuildCreateFormAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TourFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        var result = await _tourService.CreateAsync(vm);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể tạo tour.");
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã tạo tour mới.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _tourService.GetEditFormAsync(id);
        return vm is null ? NotFound() : View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TourFormViewModel vm)
    {
        if (id != vm.Id)
            return NotFound();

        if (!ModelState.IsValid)
        {
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        var result = await _tourService.UpdateAsync(id, vm);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể cập nhật tour.");
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã cập nhật tour.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _tourService.GetDeleteModelAsync(id);
        return entity is null ? NotFound() : View(entity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _tourService.DeleteAsync(id);
        TempData["SuccessMessage"] = "Đã xóa tour.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<TourFormViewModel> RebuildFormAsync(TourFormViewModel vm)
    {
        var freshVm = await _tourService.BuildCreateFormAsync();
        freshVm.Id = vm.Id;
        freshVm.Name = vm.Name;
        freshVm.Description = vm.Description;
        freshVm.IsActive = vm.IsActive;
        freshVm.TtsScriptVi = vm.TtsScriptVi;
        freshVm.TtsScriptEn = vm.TtsScriptEn;
        freshVm.TtsScriptZh = vm.TtsScriptZh;
        freshVm.TtsScriptJa = vm.TtsScriptJa;
        freshVm.TtsScriptDe = vm.TtsScriptDe;
        freshVm.Stops = vm.Stops.Count == 0
            ? freshVm.Stops
            : vm.Stops;
        return freshVm;
    }
}
