using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace VKFoodArea.Web.Services;

public class PoiAudioStorageService : IPoiAudioStorageService
{
    private static readonly HashSet<string> AllowedExtensions =
    [
        ".mp3",
        ".m4a",
        ".aac",
        ".wav",
        ".ogg"
    ];

    private const long MaxFileSizeBytes = 24 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment;

    public PoiAudioStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string? Validate(IFormFile? audioFile)
    {
        if (audioFile is null)
            return null;

        if (audioFile.Length <= 0)
            return "File audio dang rong.";

        if (audioFile.Length > MaxFileSizeBytes)
            return "File audio qua lon. Vui long chon file toi da 24MB.";

        var extension = Path.GetExtension(audioFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedExtensions.Contains(extension.ToLowerInvariant()))
        {
            return "Chi ho tro audio MP3, M4A, AAC, WAV hoac OGG.";
        }

        return null;
    }

    public async Task<string> SaveAsync(IFormFile audioFile, string poiName, string language, CancellationToken ct = default)
    {
        var validationError = Validate(audioFile);
        if (!string.IsNullOrWhiteSpace(validationError))
            throw new InvalidOperationException(validationError);

        var extension = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
        var fileName = BuildFileName(poiName, language, extension);
        var uploadDirectory = Path.Combine(
            _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"),
            "uploads",
            "poi-audio");

        Directory.CreateDirectory(uploadDirectory);

        await using var fileStream = File.Create(Path.Combine(uploadDirectory, fileName));
        await audioFile.CopyToAsync(fileStream, ct);

        return $"/uploads/poi-audio/{fileName}";
    }

    private static string BuildFileName(string? poiName, string? language, string extension)
    {
        var normalizedName = BuildAssetSegment(poiName);
        var normalizedLanguage = BuildAssetSegment(language);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

        return $"poi_{normalizedName}_{normalizedLanguage}_{timestamp}_audio{extension}";
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
