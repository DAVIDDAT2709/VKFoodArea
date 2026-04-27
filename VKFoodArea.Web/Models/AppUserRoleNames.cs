namespace VKFoodArea.Web.Models;

public static class AppUserRoleNames
{
    public const string Guest = "Guest";
    public const string User = "User";
    public const string Operator = "Operator";
    public const string Admin = "Admin";

    public static string Normalize(string? role)
    {
        var normalized = (role ?? string.Empty).Trim();

        if (normalized.Equals(Admin, StringComparison.OrdinalIgnoreCase))
            return Admin;

        if (normalized.Equals(Operator, StringComparison.OrdinalIgnoreCase))
            return Operator;

        if (normalized.Equals(User, StringComparison.OrdinalIgnoreCase))
            return User;

        return Guest;
    }

    public static string DisplayName(string? role)
    {
        return Normalize(role) switch
        {
            Admin => "Quan tri vien",
            Operator => "Dieu phoi noi bo",
            User => "Nguoi dung",
            _ => "Khach"
        };
    }
}
