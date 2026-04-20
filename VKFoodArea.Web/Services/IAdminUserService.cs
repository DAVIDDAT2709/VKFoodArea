using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public interface IAdminUserService
{
    Task<AdminUser?> AuthenticateAsync(string? username, string? password);
    Task<List<AdminUser>> GetAllAsync();
    Task<AdminUserFormViewModel?> GetEditFormAsync(int id);
    Task<AdminUser?> GetDeleteModelAsync(int id);
    Task<AdminUserSaveResult> CreateAsync(AdminUserFormViewModel vm, string? currentUsername);
    Task<AdminUserSaveResult> UpdateAsync(int id, AdminUserFormViewModel vm, string? currentUsername);
    Task<AdminUserSaveResult> DeleteAsync(int id, string? currentUsername);
    Task<AdminUserIndexViewModel> GetIndexAsync(string? query, string? role, string? status, int page = 1);
    Task<bool> ResetPasswordAsync(int id, string? currentUsername, string newPassword = "123456");
}
