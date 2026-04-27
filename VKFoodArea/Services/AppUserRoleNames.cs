namespace VKFoodArea.Services;

public static class AppUserRoleNames
{
    public const string Guest = "Guest";
    public const string User = "User";
    public const string Operator = "Operator";
    public const string Admin = "Admin";

    public static string Normalize(string? role)
    {
        var normalized = (role ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return Guest;

        if (normalized.Equals(Admin, StringComparison.OrdinalIgnoreCase))
            return Admin;

        if (normalized.Equals(Operator, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Staff", StringComparison.OrdinalIgnoreCase))
        {
            return Operator;
        }

        if (normalized.Equals(User, StringComparison.OrdinalIgnoreCase))
            return User;

        return User;
    }
}
