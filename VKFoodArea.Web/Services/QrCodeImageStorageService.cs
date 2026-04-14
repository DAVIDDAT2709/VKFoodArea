using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public class QrCodeImageStorageService : IQrCodeImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    private const long MaxFileSizeBytes = 8 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public QrCodeImageStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string? Validate(IFormFile? imageFile)
    {
        if (imageFile is null)
            return null;

        if (imageFile.Length <= 0)
            return "Ảnh QR đang rỗng.";

        if (imageFile.Length > MaxFileSizeBytes)
            return "Ảnh QR quá lớn. Vui lòng chọn file tối đa 8MB.";

        var extension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension.ToLowerInvariant()))
        {
            return "Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP.";
        }

        return null;
    }

    public async Task<string> SaveAsync(IFormFile imageFile, string qrCode, CancellationToken ct = default)
    {
        var validationError = Validate(imageFile);
        if (!string.IsNullOrWhiteSpace(validationError))
            throw new InvalidOperationException(validationError);

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var fileName = BuildFileName(qrCode, extension);
        var uploadsDirectory = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            "qr-images");

        Directory.CreateDirectory(uploadsDirectory);

        await using var stream = new FileStream(
            Path.Combine(uploadsDirectory, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await imageFile.CopyToAsync(stream, ct);
        return $"/uploads/qr-images/{fileName}";
    }

    private static string BuildFileName(string? qrCode, string extension)
    {
        var normalizedCode = BuildAssetSegment(qrCode);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        return $"qr_{normalizedCode}_{timestamp}{extension}";
    }

    private static string BuildAssetSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "qr";

        var builder = new StringBuilder(text.Length);
        var previousWasUnderscore = false;

        foreach (var character in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) && character <= '\x7f')
            {
                builder.Append(character);
                previousWasUnderscore = false;
                continue;
            }

            if (previousWasUnderscore)
                continue;

            builder.Append('_');
            previousWasUnderscore = true;
        }

        var slug = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "qr" : slug;
    }
}
