using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize]
public class QrCodeItemsController : Controller
{
    private readonly IQrCodeItemService _qrCodeItemService;

    public QrCodeItemsController(IQrCodeItemService qrCodeItemService)
    {
        _qrCodeItemService = qrCodeItemService;
    }

    public async Task<IActionResult> Index()
    {
        var data = await _qrCodeItemService.GetAllAsync();
        return View(data);
    }

    public async Task<IActionResult> Create()
    {
        var vm = await _qrCodeItemService.BuildCreateFormAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QrCodeItemFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        var result = await _qrCodeItemService.CreateAsync(vm);

        if (!result.Success)
        {
            ModelState.AddModelError(nameof(vm.Code), result.Error!);
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã tạo QR code mới.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _qrCodeItemService.GetEditFormAsync(id);
        if (vm is null) return NotFound();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, QrCodeItemFormViewModel vm)
    {
        if (id != vm.Id) return NotFound();

        if (!ModelState.IsValid)
        {
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        var result = await _qrCodeItemService.UpdateAsync(id, vm);

        if (!result.Success)
        {
            ModelState.AddModelError(nameof(vm.Code), result.Error!);
            vm = await RebuildFormAsync(vm);
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã cập nhật QR code.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await _qrCodeItemService.GetDeleteModelAsync(id);
        if (item is null) return NotFound();

        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _qrCodeItemService.DeleteAsync(id);

        TempData["SuccessMessage"] = "Đã xóa QR code.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<QrCodeItemFormViewModel> RebuildFormAsync(QrCodeItemFormViewModel vm)
    {
        var freshVm = await _qrCodeItemService.BuildCreateFormAsync();

        freshVm.Id = vm.Id;
        freshVm.Code = vm.Code;
        freshVm.Title = vm.Title;
        freshVm.PoiId = vm.PoiId;
        freshVm.IsActive = vm.IsActive;

        return freshVm;
    }
}
