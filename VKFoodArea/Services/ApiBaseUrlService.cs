namespace VKFoodArea.Services;

public class ApiBaseUrlService
{
    private const string LegacyOcDao2Image = "ocdao2.jpg";
    private const string SafeOcDaoImage = "ocdao_img.jpg";
    private static readonly string[] AppLinkSourceKeys = ["source", "baseUrl", "origin"];

    private readonly AppSettingsService _settingsService;
    private readonly AppBuildMetadataService _buildMetadataService;

    public ApiBaseUrlService(
        AppSettingsService settingsService,
        AppBuildMetadataService buildMetadataService)
    {
        _settingsService = settingsService;
        _buildMetadataService = buildMetadataService;
    }

    public string BaseUrl
        => !string.IsNullOrWhiteSpace(ManualDemoBaseUrl)
            ? ManualDemoBaseUrl
            : !string.IsNullOrWhiteSpace(OfficialReleaseBaseUrl)
                ? OfficialReleaseBaseUrl
                : AutoDetectedBaseUrl;

    public string ManualDemoBaseUrl
        => CanUseDemoTools
            ? NormalizeBaseUrl(_settingsService.ApiBaseUrl, string.Empty)
            : string.Empty;

    public string OfficialReleaseBaseUrl
        => _buildMetadataService.OfficialBaseUrl;

    public string AutoDetectedBaseUrl
        => NormalizeBaseUrl(_settingsService.AutoDetectedApiBaseUrl, string.Empty);

    public bool CanUseDemoTools => _buildMetadataService.DemoToolsEnabled;

    public bool HasConfiguredBaseUrl => !string.IsNullOrWhiteSpace(BaseUrl);

    public bool HasOfficialReleaseBaseUrl => _buildMetadataService.HasOfficialBaseUrl;

    public bool IsUsingManualDemoBaseUrl
        => !string.IsNullOrWhiteSpace(ManualDemoBaseUrl) &&
           string.Equals(BaseUrl, ManualDemoBaseUrl, StringComparison.OrdinalIgnoreCase);

    public bool IsUsingOfficialReleaseBaseUrl
        => !string.IsNullOrWhiteSpace(OfficialReleaseBaseUrl) &&
           string.Equals(BaseUrl, OfficialReleaseBaseUrl, StringComparison.OrdinalIgnoreCase);

    public bool IsUsingAutoDetectedBaseUrl
        => !string.IsNullOrWhiteSpace(AutoDetectedBaseUrl) &&
           string.Equals(BaseUrl, AutoDetectedBaseUrl, StringComparison.OrdinalIgnoreCase);

    public (bool Success, string Message) SaveDemoBaseUrl(string? value)
    {
        if (!CanUseDemoTools)
            return (false, "Bản này không mở mục này.");

        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _settingsService.ApiBaseUrl = string.Empty;

            if (!string.IsNullOrWhiteSpace(OfficialReleaseBaseUrl))
                return (true, "Đã quay về địa chỉ mặc định.");

            return !string.IsNullOrWhiteSpace(AutoDetectedBaseUrl)
                ? (true, "Đã quay về địa chỉ từ liên kết gần nhất.")
                : (true, "Đã xóa địa chỉ tạm.");
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "Địa chỉ phải bắt đầu bằng http:// hoặc https://.");
        }

        _settingsService.ApiBaseUrl = NormalizeBaseUrl(normalized, string.Empty);
        return (true, "Đã cập nhật địa chỉ web.");
    }

    public bool TryBuildApiUrl(string relativePath, out string url)
    {
        var normalizedBaseUrl = BaseUrl;
        if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
        {
            url = string.Empty;
            return false;
        }

        var normalizedPath = (relativePath ?? string.Empty).Trim();
        normalizedPath = normalizedPath.TrimStart('/');

        url = new Uri(new Uri(normalizedBaseUrl), normalizedPath).ToString();
        return true;
    }

    public string BuildApiUrl(string relativePath)
        => TryBuildApiUrl(relativePath, out var url)
            ? url
            : throw new RemoteEndpointUnavailableException();

    public string ResolveImageUrl(string? imageUrl)
    {
        var normalized = (imageUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
            return normalized;

        if (normalized.StartsWith('/'))
            return ResolveRemoteUrl(normalized);

        if (normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return ResolveRemoteUrl(normalized);

        return ResolveLegacyLocalImage(normalized);
    }

    public bool TryCaptureBaseUrlFromUri(Uri? uri)
    {
        var capturedBaseUrl = ExtractBaseUrl(uri);
        if (string.IsNullOrWhiteSpace(capturedBaseUrl))
            return false;

        var normalized = NormalizeBaseUrl(capturedBaseUrl, string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (string.Equals(_settingsService.AutoDetectedApiBaseUrl, normalized, StringComparison.OrdinalIgnoreCase))
            return false;

        _settingsService.AutoDetectedApiBaseUrl = normalized;
        return true;
    }

    public string ResolveRemoteUrl(string? path)
    {
        var normalized = (path ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
            return normalized;

        return TryBuildApiUrl(normalized, out var url)
            ? url
            : string.Empty;
    }

    private static string ExtractBaseUrl(Uri? uri)
    {
        if (uri is null)
            return string.Empty;

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeBaseUrl(uri.GetLeftPart(UriPartial.Authority), string.Empty);
        }

        foreach (var key in AppLinkSourceKeys)
        {
            var candidate = GetQueryParameter(uri, key);
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var sourceUri) &&
                (sourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                 sourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return NormalizeBaseUrl(sourceUri.GetLeftPart(UriPartial.Authority), string.Empty);
            }
        }

        return string.Empty;
    }

    private static string? GetQueryParameter(Uri uri, string parameterName)
    {
        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
            return null;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0 ||
                !parts[0].Equals(parameterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length == 2
                ? Uri.UnescapeDataString(parts[1])
                : string.Empty;
        }

        return null;
    }

    private static string NormalizeBaseUrl(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return fallback;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var authorityOnly = uri.GetLeftPart(UriPartial.Authority);
            normalized = authorityOnly.EndsWith('/')
                ? authorityOnly
                : $"{authorityOnly}/";
            return normalized;
        }

        if (!normalized.EndsWith('/'))
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
