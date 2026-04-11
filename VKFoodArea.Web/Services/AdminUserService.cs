using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _context;

    public AdminUserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminUser?> AuthenticateAsync(string? username, string? password)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _context.AdminUsers
            .FirstOrDefaultAsync(x => x.Username.ToLower() == normalizedUsername && x.IsActive);

        if (user is null || !AdminPasswordHasher.Verify(password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<List<AdminUser>> GetAllAsync()
    {
        return await _context.AdminUsers
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Username)
            .ToListAsync();
    }

    public async Task<AdminUserFormViewModel?> GetEditFormAsync(int id)
    {
        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        return user is null ? null : ToForm(user);
    }

    public Task<AdminUser?> GetDeleteModelAsync(int id)
    {
        return _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<AdminUserSaveResult> CreateAsync(AdminUserFormViewModel vm)
    {
        var normalizedUsername = NormalizeUsername(vm.Username);
        if (string.IsNullOrWhiteSpace(vm.Password))
            return AdminUserSaveResult.Fail("Vui lòng nhập mật khẩu cho admin mới.");

        var duplicate = await _context.AdminUsers
            .AnyAsync(x => x.Username.ToLower() == normalizedUsername);

        if (duplicate)
            return AdminUserSaveResult.Fail("Tài khoản admin đã tồn tại.");

        _context.AdminUsers.Add(new AdminUser
        {
            Username = normalizedUsername,
            FullName = vm.FullName.Trim(),
            Role = NormalizeRole(vm.Role),
            PasswordHash = AdminPasswordHasher.Hash(vm.Password),
            IsActive = vm.IsActive,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return AdminUserSaveResult.Ok();
    }

    public async Task<AdminUserSaveResult> UpdateAsync(int id, AdminUserFormViewModel vm)
    {
        var user = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return AdminUserSaveResult.Fail("Không tìm thấy admin.");

        var normalizedUsername = NormalizeUsername(vm.Username);
        var duplicate = await _context.AdminUsers
            .AnyAsync(x => x.Id != id && x.Username.ToLower() == normalizedUsername);

        if (duplicate)
            return AdminUserSaveResult.Fail("Tài khoản admin đã tồn tại.");

        user.Username = normalizedUsername;
        user.FullName = vm.FullName.Trim();
        user.Role = NormalizeRole(vm.Role);
        user.IsActive = vm.IsActive;

        if (!string.IsNullOrWhiteSpace(vm.Password))
            user.PasswordHash = AdminPasswordHasher.Hash(vm.Password);

        await _context.SaveChangesAsync();
        return AdminUserSaveResult.Ok();
    }

    public async Task<AdminUserSaveResult> DeleteAsync(int id, string? currentUsername)
    {
        var user = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return AdminUserSaveResult.Fail("Không tìm thấy admin.");

        if (string.Equals(user.Username, NormalizeUsername(currentUsername), StringComparison.OrdinalIgnoreCase))
            return AdminUserSaveResult.Fail("Không thể xóa tài khoản đang đăng nhập.");

        _context.AdminUsers.Remove(user);
        await _context.SaveChangesAsync();
        return AdminUserSaveResult.Ok();
    }

    private static AdminUserFormViewModel ToForm(AdminUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Role = user.Role,
        IsActive = user.IsActive
    };

    private static string NormalizeUsername(string? username)
        => (username ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeRole(string? role)
        => string.IsNullOrWhiteSpace(role) ? "Admin" : role.Trim();
}
