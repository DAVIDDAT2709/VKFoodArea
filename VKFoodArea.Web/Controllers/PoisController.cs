using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

public class PoisController : Controller
{
    private readonly IPoiService _poiService;

    public PoisController(IPoiService poiService)
    {
        _poiService = poiService;
    }

    public async Task<IActionResult> Index()
    {
        var pois = await _poiService.GetAllAsync();
        return View(pois);
    }

    public IActionResult Create()
    {
        return View(new PoiFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PoiFormViewModel vm)
    {
        var qrError = await _poiService.ValidateDefaultQrCodeAsync(null, vm.QrCode);
        if (!string.IsNullOrWhiteSpace(qrError))
            ModelState.AddModelError(nameof(vm.QrCode), qrError);

        if (!ModelState.IsValid)
            return View(vm);

        var createdId = await _poiService.CreateAsync(vm);

        TempData["SuccessMessage"] = "Đã tạo POI mới và tự sinh 4 ngôn ngữ TTS.";
        return RedirectToAction(nameof(Edit), new { id = createdId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _poiService.GetEditFormAsync(id);
        if (vm is null)
            return NotFound();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PoiFormViewModel vm)
    {
        if (id != vm.Id)
            return NotFound();

        var qrError = await _poiService.ValidateDefaultQrCodeAsync(id, vm.QrCode);
        if (!string.IsNullOrWhiteSpace(qrError))
            ModelState.AddModelError(nameof(vm.QrCode), qrError);

        if (!ModelState.IsValid)
            return View(vm);

        var updated = await _poiService.UpdateAsync(id, vm);
        if (!updated)
            return NotFound();

        TempData["SuccessMessage"] = "Đã cập nhật POI và làm mới bản dịch TTS.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var poi = await _poiService.GetDeleteModelAsync(id);
        if (poi is null)
            return NotFound();

        return View(poi);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _poiService.DeleteAsync(id);

        TempData["SuccessMessage"] = "Đã xóa POI.";
        return RedirectToAction(nameof(Index));
    }
}
