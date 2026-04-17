using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
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

    [AllowAnonymous]
    [HttpGet("/qr/{code}")]
    public IActionResult Resolve(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing QR code.");

        var normalizedCode = code.Trim();
        var encodedCode = Uri.EscapeDataString(normalizedCode);
        var htmlCode = WebUtility.HtmlEncode(normalizedCode);
        var customSchemeUrl = $"vkfoodarea://qr/{encodedCode}";
        var androidIntentUrl = $"intent://qr/{encodedCode}#Intent;scheme=vkfoodarea;package=com.companyname.vkfoodarea;end";
        var apiUrl = $"/api/resolve-qr?code={encodedCode}";

        var html = $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Mo VKFoodArea</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f4f9f6;
            --card: #ffffff;
            --ink: #12352f;
            --muted: #5e746d;
            --accent: #16a34a;
            --accent-dark: #11793a;
        }

        * { box-sizing: border-box; }

        body {
            margin: 0;
            min-height: 100vh;
            display: grid;
            place-items: center;
            padding: 24px;
            font-family: "Segoe UI", Arial, sans-serif;
            color: var(--ink);
            background:
                radial-gradient(circle at top, #dff7e6 0, transparent 38%),
                linear-gradient(180deg, #f7fcf8 0%, var(--bg) 100%);
        }

        main {
            width: min(100%, 520px);
            background: var(--card);
            border-radius: 24px;
            padding: 28px;
            box-shadow: 0 20px 50px rgba(14, 46, 38, 0.12);
        }

        h1 {
            margin: 0 0 12px;
            font-size: 28px;
        }

        p {
            margin: 0 0 14px;
            line-height: 1.6;
            color: var(--muted);
        }

        .code {
            display: inline-block;
            padding: 6px 10px;
            border-radius: 999px;
            background: #edf8ef;
            color: var(--accent-dark);
            font-weight: 700;
        }

        .actions {
            display: grid;
            gap: 12px;
            margin-top: 24px;
        }

        a.button {
            display: inline-flex;
            justify-content: center;
            align-items: center;
            min-height: 48px;
            padding: 0 16px;
            border-radius: 14px;
            text-decoration: none;
            font-weight: 700;
        }

        a.primary {
            color: #fff;
            background: linear-gradient(135deg, #16a34a, #0f8b64);
        }

        a.secondary {
            color: var(--ink);
            background: #eef5f1;
        }
    </style>
</head>
<body>
    <main>
        <h1>Dang mo VKFoodArea</h1>
        <p>Neu dien thoai da cai app, lien ket nay se mo thang man hinh thuyet minh. Neu ban dang demo TTS, hay quet ma nay trong man hinh QR cua app hoac bam nut mo app ben duoi.</p>
        <p>Ma QR: <span class="code">{{htmlCode}}</span></p>
        <div class="actions">
            <a id="open-app-button" class="button primary" href="{{customSchemeUrl}}" data-android-intent="{{androidIntentUrl}}">Mo ung dung</a>
            <a class="button secondary" href="{{apiUrl}}">Xem du lieu API</a>
        </div>
    </main>
    <script>
        var openAppButton = document.getElementById('open-app-button');
        var isAndroid = /android/i.test(window.navigator.userAgent || '');
        var launchUrl = isAndroid
            ? openAppButton.getAttribute('data-android-intent')
            : '{{customSchemeUrl}}';

        openAppButton.setAttribute('href', launchUrl);

        window.setTimeout(function () {
            window.location.replace(launchUrl);
        }, 250);
    </script>
</body>
</html>
""";

        return Content(html, "text/html; charset=utf-8");
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
        var imageError = _qrCodeItemService.ValidateImageFile(vm.ImageFile);
        if (!string.IsNullOrWhiteSpace(imageError))
            ModelState.AddModelError(nameof(vm.ImageFile), imageError);

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

        var imageError = _qrCodeItemService.ValidateImageFile(vm.ImageFile);
        if (!string.IsNullOrWhiteSpace(imageError))
            ModelState.AddModelError(nameof(vm.ImageFile), imageError);

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
        freshVm.CurrentImageUrl = vm.CurrentImageUrl;
        freshVm.TargetType = vm.TargetType;
        freshVm.PoiId = vm.PoiId;
        freshVm.TourId = vm.TourId;
        freshVm.IsActive = vm.IsActive;

        return freshVm;
    }
}
