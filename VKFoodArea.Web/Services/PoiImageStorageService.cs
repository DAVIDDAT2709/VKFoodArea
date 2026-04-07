using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public class PoiImageStorageService : IPoiImageStorageService
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

    public PoiImageStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string? Validate(IFormFile? imageFile)
    {
        if (imageFile is null)
            return null;

        if (imageFile.Length <= 0)
            return "Ảnh tải lên đang rỗng.";

        if (imageFile.Length > MaxFileSizeBytes)
            return "Ảnh tải lên quá lớn. Vui lòng chọn file tối đa 8MB.";

        var extension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension.ToLowerInvariant()))
        {
            return "Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP.";
        }

        return null;
    }

    public async Task<string> SaveAsync(IFormFile imageFile, string poiName, CancellationToken ct = default)
    {
        var validationError = Validate(imageFile);
        if (!string.IsNullOrWhiteSpace(validationError))
            throw new InvalidOperationException(validationError);

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var fileName = BuildFileName(poiName, extension);

        var webUploadsDirectory = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            "poi-images");

        var appImagesDirectory = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "..",
            "VKFoodArea",
            "Resources",
            "Images"));

        Directory.CreateDirectory(webUploadsDirectory);
        Directory.CreateDirectory(appImagesDirectory);

        await using var memoryStream = new MemoryStream();
        await imageFile.CopyToAsync(memoryStream, ct);
        var bytes = memoryStream.ToArray();

        await File.WriteAllBytesAsync(Path.Combine(webUploadsDirectory, fileName), bytes, ct);
        await File.WriteAllBytesAsync(Path.Combine(appImagesDirectory, fileName), bytes, ct);

        return $"/uploads/poi-images/{fileName}";
    }

    private static string BuildFileName(string? poiName, string extension)
    {
        var normalizedName = BuildAssetSegment(poiName);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

        return $"poi_{normalizedName}_{timestamp}_img{extension}";
    }

    private static string BuildAssetSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "poi";

        var builder = new StringBuilder(text.Length);
        var previousWasUnderscore = false;

        foreach (var character in text
                     .Trim()
                     .ToLowerInvariant()
                     .Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var normalizedCharacter = character switch
            {
                '\u0111' => 'd',
                _ => character
            };

            if (char.IsLetterOrDigit(normalizedCharacter) && normalizedCharacter <= '\x7f')
            {
                builder.Append(normalizedCharacter);
                previousWasUnderscore = false;
                continue;
            }

            if (previousWasUnderscore)
                continue;

            builder.Append('_');
            previousWasUnderscore = true;
        }

        var slug = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "poi" : slug;
    }
}
