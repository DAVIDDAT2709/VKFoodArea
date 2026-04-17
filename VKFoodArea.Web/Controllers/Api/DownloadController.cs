using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VKFoodArea.Web.Controllers;

[AllowAnonymous]
public class DownloadController : Controller
{
    private readonly IWebHostEnvironment _env;
    private const string ApkFileName = "com.companyname.vkfoodarea-Signed.apk";

    public DownloadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpGet("/download-app")]
    public IActionResult Index()
    {
        var apkUrl = Url.Action(nameof(Apk), "Download", new { v = GetApkVersionToken() }) ?? "/download-apk";

        var html = $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Tải VKFoodArea</title>
    <style>
        body {
            margin: 0;
            font-family: Arial, sans-serif;
            background: #f5f7fb;
            color: #1f2937;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 24px;
        }

        .card {
            width: 100%;
            max-width: 520px;
            background: white;
            border-radius: 20px;
            padding: 28px;
            box-shadow: 0 10px 30px rgba(0,0,0,.08);
        }

        h1 {
            margin-top: 0;
            margin-bottom: 12px;
            font-size: 28px;
        }

        p {
            line-height: 1.6;
            color: #4b5563;
        }

        .btn {
            display: inline-block;
            margin-top: 16px;
            padding: 14px 18px;
            border-radius: 12px;
            text-decoration: none;
            font-weight: 700;
        }

        .btn-primary {
            background: #16a34a;
            color: white;
        }

        .btn-secondary {
            background: #eef2ff;
            color: #1f2937;
            margin-left: 8px;
        }

        .note {
            margin-top: 18px;
            font-size: 14px;
            color: #6b7280;
        }

        code {
            background: #f3f4f6;
            padding: 2px 6px;
            border-radius: 6px;
        }
    </style>
</head>
<body>
    <div class="card">
        <h1>Tải ứng dụng VKFoodArea</h1>
        <p>Nhấn nút bên dưới để tải file APK về điện thoại Android.</p>
        <a class="btn btn-primary" href="{{apkUrl}}">Tải APK</a>
        <a class="btn btn-secondary" href="/">Về trang chủ</a>

        <div class="note">
            Tên file: <code>{{ApkFileName}}</code><br />
            Sau khi tải xong, mở file APK để cài đặt ứng dụng.
        </div>
    </div>
</body>
</html>
""";

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("/download-apk")]
    public IActionResult Apk()
    {
        var apkPath = Path.Combine(_env.WebRootPath, "apk", ApkFileName);

        if (!System.IO.File.Exists(apkPath))
            return NotFound("Không tìm thấy file APK.");

        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        return PhysicalFile(
            apkPath,
            "application/vnd.android.package-archive",
            ApkFileName);
    }

    private string GetApkVersionToken()
    {
        var apkPath = Path.Combine(_env.WebRootPath, "apk", ApkFileName);
        if (!System.IO.File.Exists(apkPath))
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        var file = new FileInfo(apkPath);
        return $"{file.LastWriteTimeUtc:yyyyMMddHHmmss}-{file.Length}";
    }
}
