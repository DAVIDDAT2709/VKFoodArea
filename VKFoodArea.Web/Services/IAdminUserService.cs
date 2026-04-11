using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IAdminUserService
{
    Task<AdminUser?> AuthenticateAsync(string? username, string? password);
    Task<List<AdminUser>> GetAllAsync();
    Task<AdminUserFormViewModel?> GetEditFormAsync(int id);
    Task<AdminUser?> GetDeleteModelAsync(int id);
    Task<AdminUserSaveResult> CreateAsync(AdminUserFormViewModel vm);
    Task<AdminUserSaveResult> UpdateAsync(int id, AdminUserFormViewModel vm);
    Task<AdminUserSaveResult> DeleteAsync(int id, string? currentUsername);
}
