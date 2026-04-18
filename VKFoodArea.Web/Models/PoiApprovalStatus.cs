namespace VKFoodArea.Web.Models;

public static class PoiApprovalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static string Normalize(string? status)
    {
        var normalized = (status ?? string.Empty).Trim();

        if (normalized.Equals(Pending, StringComparison.OrdinalIgnoreCase))
            return Pending;

        if (normalized.Equals(Rejected, StringComparison.OrdinalIgnoreCase))
            return Rejected;

        return Approved;
    }

    public static bool IsApproved(string? status)
        => Normalize(status) == Approved;

    public static string DisplayName(string? status)
        => Normalize(status) switch
        {
            Pending => "Chờ duyệt",
            Rejected => "Từ chối",
            _ => "Đã duyệt"
        };
}
