namespace VKFoodArea.Services;

public sealed class AppAuthorizationService
{
    private readonly AuthService _authService;
    private readonly AppBuildMetadataService _buildMetadataService;

    public AppAuthorizationService(
        AuthService authService,
        AppBuildMetadataService buildMetadataService)
    {
        _authService = authService;
        _buildMetadataService = buildMetadataService;
    }

    public bool IsAuthenticated => _authService.CurrentUser is not null;

    public bool InternalToolsEnabled => _buildMetadataService.InternalToolsEnabled;

    public string CurrentRole => _authService.CurrentUser is null
        ? AppUserRoleNames.Guest
        : AppUserRoleNames.Normalize(_authService.CurrentUser.Role);

    public bool CanUseInternalTools
        => AppFeatureAccessPolicy.CanUseInternalTools(CurrentRole, InternalToolsEnabled);

    public bool CanOverrideRemoteEndpoint
        => AppFeatureAccessPolicy.CanOverrideRemoteEndpoint(CurrentRole, InternalToolsEnabled);
}
