using System.Security.Claims;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.Services;

public class CurrentAdminService : ICurrentAdminService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAdminService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string Role
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return string.Empty;

            return AdminRoleNames.Normalize(user.FindFirstValue(ClaimTypes.Role));
        }
    }

    public bool IsAdmin => Role == AdminRoleNames.Admin;

    public bool IsRestaurantOwner => Role == AdminRoleNames.RestaurantOwner;
}
