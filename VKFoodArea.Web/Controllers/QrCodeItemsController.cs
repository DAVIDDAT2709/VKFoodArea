using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

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
            vm = await RebuildCreateFormAsync(vm);
            return View(vm);
        }

        var result = await _qrCodeItemService.CreateAsync(vm);

        if (!result.Success)
        {
            ModelState.AddModelError(nameof(vm.Code), result.Error!);
            vm = await RebuildCreateFormAsync(vm);
            return View(vm);
        }

        TempData["SuccessMessage"] = "Đã tạo QR code.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<QrCodeItemFormViewModel> RebuildCreateFormAsync(QrCodeItemFormViewModel vm)
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