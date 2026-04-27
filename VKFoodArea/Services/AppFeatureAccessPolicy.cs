namespace VKFoodArea.Services;

public static class AppFeatureAccessPolicy
{
    public static bool CanUseInternalTools(string? role, bool internalToolsEnabled)
    {
        if (!internalToolsEnabled)
            return false;

        var normalizedRole = AppUserRoleNames.Normalize(role);
        return normalizedRole is AppUserRoleNames.Operator or AppUserRoleNames.Admin;
    }

    public static bool CanOverrideRemoteEndpoint(string? role, bool internalToolsEnabled)
    {
        if (!internalToolsEnabled)
            return false;

        return AppUserRoleNames.Normalize(role) == AppUserRoleNames.Admin;
    }
}
