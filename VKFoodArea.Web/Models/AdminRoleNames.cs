namespace VKFoodArea.Web.Models;

public static class AdminRoleNames
{
    public const string Admin = "Admin";
    public const string RestaurantOwner = "RestaurantOwner";
    public const string AdminOnly = Admin;
    public const string AdminOrRestaurantOwner = $"{Admin},{RestaurantOwner}";

    public static string Normalize(string? role)
    {
        var normalized = (role ?? string.Empty).Trim();

        if (normalized.Equals(RestaurantOwner, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Owner", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Restaurant", StringComparison.OrdinalIgnoreCase))
        {
            return RestaurantOwner;
        }

        return Admin;
    }

    public static string DisplayName(string? role)
        => Normalize(role) == RestaurantOwner ? "Chủ nhà hàng" : "Admin";
}
