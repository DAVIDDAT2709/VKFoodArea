using Microsoft.Maui.Devices;

namespace VKFoodArea.Services;

public class ApiBaseUrlService
{
    private const string LegacyOcDao2Image = "ocdao2.jpg";
    private const string SafeOcDaoImage = "ocdao_img.jpg";

    public string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5216/"
            : "http://localhost:5216/";

    public string ResolveImageUrl(string? imageUrl)
    {
        var normalized = (imageUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
            return normalized;

        if (normalized.StartsWith("/", StringComparison.Ordinal))
            return new Uri(new Uri(BaseUrl), normalized.TrimStart('/')).ToString();

        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return new Uri(new Uri(BaseUrl), normalized).ToString();

        return ResolveLegacyLocalImage(normalized);
    }

    private static string ResolveLegacyLocalImage(string imageUrl)
    {
        if (imageUrl.Equals(LegacyOcDao2Image, StringComparison.OrdinalIgnoreCase))
            return SafeOcDaoImage;

        return imageUrl;
    }
}
