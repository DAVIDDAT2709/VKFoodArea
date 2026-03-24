namespace VKFoodArea.Web.Helpers;

public static class QrCodeHelper
{
    public static string Normalize(string? code)
    {
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }
}