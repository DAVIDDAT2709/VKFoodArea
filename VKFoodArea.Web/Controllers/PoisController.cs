using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize]
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

        var imageError = _poiService.ValidateImageFile(vm.ImageFile);
        if (!string.IsNullOrWhiteSpace(imageError))
            ModelState.AddModelError(nameof(vm.ImageFile), imageError);

        AddAudioValidationErrors(vm);

        if (!ModelState.IsValid)
            return View(vm);

        var createdId = await _poiService.CreateAsync(vm);

        TempData["SuccessMessage"] = "Đã tạo POI mới, ảnh đã được lưu và TTS đã được sinh tự động.";
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

        var imageError = _poiService.ValidateImageFile(vm.ImageFile);
        if (!string.IsNullOrWhiteSpace(imageError))
            ModelState.AddModelError(nameof(vm.ImageFile), imageError);

        AddAudioValidationErrors(vm);

        if (!ModelState.IsValid)
            return View(vm);

        var updated = await _poiService.UpdateAsync(id, vm);
        if (!updated)
            return NotFound();

        TempData["SuccessMessage"] = "Đã cập nhật POI, ảnh và bản dịch TTS đã được làm mới.";
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

    private void AddAudioValidationErrors(PoiFormViewModel vm)
    {
        AddAudioValidationError(nameof(vm.AudioFileViUpload), vm.AudioFileViUpload);
        AddAudioValidationError(nameof(vm.AudioFileEnUpload), vm.AudioFileEnUpload);
        AddAudioValidationError(nameof(vm.AudioFileJaUpload), vm.AudioFileJaUpload);
    }

    private void AddAudioValidationError(string fieldName, IFormFile? audioFile)
    {
        var audioError = _poiService.ValidateAudioFile(audioFile);
        if (!string.IsNullOrWhiteSpace(audioError))
            ModelState.AddModelError(fieldName, audioError);
    }
}
