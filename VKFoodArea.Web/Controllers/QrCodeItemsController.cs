using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Net;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Controllers;

[Authorize(Roles = AdminRoleNames.AdminOnly)]
public class QrCodeItemsController : Controller
{
    private readonly IQrCodeItemService _qrCodeItemService;
    private readonly IConfiguration _configuration;

    public QrCodeItemsController(
        IQrCodeItemService qrCodeItemService,
        IConfiguration configuration)
    {
        _qrCodeItemService = qrCodeItemService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var data = await _qrCodeItemService.GetAllAsync();
        var vm = new QrCodeItemIndexViewModel
        {
            Items = PagedListViewModel<QrCodeItemListItemViewModel>.Create(data, page),
            TotalCount = data.Count,
            ActiveCount = data.Count(x => x.IsActive),
            CoveredPoiCount = data
                .Where(x => string.Equals(x.TargetType, QrTargetTypes.Poi, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.TargetId)
                .Distinct()
                .Count(),
            CoveredTourCount = data
                .Where(x => string.Equals(x.TargetType, QrTargetTypes.Tour, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.TargetId)
                .Distinct()
                .Count()
        };

        return View(vm);
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
        var requestBaseUrl = BuildRequestBaseUrl();
        var encodedSourceBaseUrl = Uri.EscapeDataString(requestBaseUrl);
        var customSchemeUrl = $"vkfoodarea://qr/{encodedCode}?source={encodedSourceBaseUrl}";
        var androidIntentUrl = $"intent://qr/{encodedCode}?source={encodedSourceBaseUrl}#Intent;scheme=vkfoodarea;package=com.companyname.vkfoodarea;end";

        var html = $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Mở VKFoodArea</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f3f7f5;
            --card: #ffffff;
            --ink: #12352f;
            --muted: #5e746d;
            --accent: #0f8f62;
            --accent-soft: #e8f5ef;
            --border: #d7e6df;
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
            border-radius: 16px;
            border: 1px solid var(--border);
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
            background: var(--accent-soft);
            color: var(--accent);
            font-weight: 700;
        }

        .note {
            margin-top: 18px;
            padding: 14px 16px;
            border-radius: 10px;
            background: #f8fbf9;
            border: 1px solid var(--border);
            font-size: 14px;
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
            border-radius: 8px;
            text-decoration: none;
            font-weight: 700;
        }

        a.primary {
            color: #fff;
            background: var(--accent);
        }

        a.secondary {
            color: var(--ink);
            background: #eef5f1;
            border: 1px solid var(--border);
        }
    </style>
</head>
<body>
    <main>
        <h1>Đang mở VKFoodArea</h1>
        <p>Nếu điện thoại đã cài app, liên kết này sẽ mở thẳng đúng nội dung của mã QR. Nếu app chưa bật lên, hãy bấm nút bên dưới.</p>
        <p>Mã QR: <span class="code">{{htmlCode}}</span></p>
        <div class="note">
            Cùng một mã này dùng được cho camera ngoài app và camera trong app. Nếu quét bằng camera trong app, hãy mở QR trên màn hình khác hoặc in ra để camera nhìn thấy.
        </div>
        <div class="actions">
            <a id="open-app-button" class="button primary" href="{{customSchemeUrl}}" data-android-intent="{{androidIntentUrl}}">Mở ứng dụng</a>
            <a class="button secondary" href="{{requestBaseUrl}}">Quay lại website</a>
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

    [AllowAnonymous]
    [HttpGet("/qr-image/{code}")]
    public IActionResult Image(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing QR code.");

        var normalizedCode = code.Trim();
        var targetUrl = $"{BuildPublicBaseUrl()}/qr/{Uri.EscapeDataString(normalizedCode)}";
        var svg = QrSvgBuilder.BuildSvg(targetUrl);
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    private string BuildPublicBaseUrl()
    {
        var configuredBaseUrl = (_configuration["PublicBaseUrl"] ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? BuildRequestBaseUrl()
            : configuredBaseUrl.TrimEnd('/');
    }

    private string BuildRequestBaseUrl()
    {
        var forwardedScheme = GetFirstForwardedValue("X-Forwarded-Proto");
        var forwardedHost = GetFirstForwardedValue("X-Forwarded-Host");
        var scheme = string.IsNullOrWhiteSpace(forwardedScheme) ? Request.Scheme : forwardedScheme;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? Request.Host.Value : forwardedHost;
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private string GetFirstForwardedValue(string headerName)
    {
        if (!Request.Headers.TryGetValue(headerName, out StringValues values))
            return string.Empty;

        var rawValue = values.FirstOrDefault() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        return rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
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
        freshVm.TargetType = vm.TargetType;
        freshVm.PoiId = vm.PoiId;
        freshVm.TourId = vm.TourId;
        freshVm.IsActive = vm.IsActive;

        return freshVm;
    }
}
