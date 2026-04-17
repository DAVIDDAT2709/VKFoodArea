namespace VKFoodArea.Web.Services;

public interface ICurrentAdminService
{
    int? UserId { get; }
    string Role { get; }
    bool IsAdmin { get; }
    bool IsRestaurantOwner { get; }
}
