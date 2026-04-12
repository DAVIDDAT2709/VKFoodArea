using Microsoft.Maui.Devices;

namespace VKFoodArea.Services;

public class ApiBaseUrlService
{
    private const string LegacyOcDao2Image = "ocdao2.jpg";
    private const string SafeOcDaoImage = "ocdao_img.jpg";

    private readonly AppSettingsService _settingsService;

    public ApiBaseUrlService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string BaseUrl => NormalizeBaseUrl(_settingsService.ApiBaseUrl, DefaultBaseUrl);

    public string DefaultBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5216/"
            : "http://localhost:5216/";

    public (bool Success, string Message) SaveDemoBaseUrl(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _settingsService.ApiBaseUrl = string.Empty;
            return (true, $"Da dat lai API ve mac dinh: {DefaultBaseUrl}");
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "URL API phai bat dau bang http:// hoac https://.");
        }

        _settingsService.ApiBaseUrl = NormalizeBaseUrl(normalized, DefaultBaseUrl);
        return (true, $"Da luu API demo: {BaseUrl}");
    }

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

    private static string NormalizeBaseUrl(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return fallback;

        if (!normalized.EndsWith("/", StringComparison.Ordinal))
            normalized += "/";

        return normalized;
    }

    private static string ResolveLegacyLocalImage(string imageUrl)
    {
        if (imageUrl.Equals(LegacyOcDao2Image, StringComparison.OrdinalIgnoreCase))
            return SafeOcDaoImage;

        return imageUrl;
    }
}
