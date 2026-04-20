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

        await AddAuditLogAsync(
            actorUsername: user.Username,
            action: "login_success",
            entityType: nameof(AdminUser),
            entityKey: user.Id.ToString(),
            note: $"Tài khoản {user.Username} đăng nhập thành công.");

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

    public async Task<AdminUserIndexViewModel> GetIndexAsync(string? query, string? role, string? status, int page = 1)
    {
        var normalizedQuery = NormalizeSearch(query);
        var normalizedRole = NormalizeRoleFilter(role);
        var normalizedStatus = NormalizeStatus(status);

        var usersQuery = _context.AdminUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            usersQuery = usersQuery.Where(x =>
                x.Username.ToLower().Contains(normalizedQuery) ||
                x.FullName.ToLower().Contains(normalizedQuery));
        }

        if (!string.IsNullOrWhiteSpace(normalizedRole))
            usersQuery = usersQuery.Where(x => x.Role == normalizedRole);

        if (normalizedStatus == "active")
            usersQuery = usersQuery.Where(x => x.IsActive);
        else if (normalizedStatus == "locked")
            usersQuery = usersQuery.Where(x => !x.IsActive);

        var users = await usersQuery
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Role)
            .ThenBy(x => x.Username)
            .Select(x => new AdminUserListItemViewModel
            {
                Id = x.Id,
                Username = x.Username,
                FullName = x.FullName,
                Role = x.Role,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt,
                OwnedPoiCount = x.Role == AdminRoleNames.RestaurantOwner
                    ? _context.Pois.Count(p => p.OwnerAdminUserId == x.Id)
                    : 0
            })
            .ToListAsync();

        var recentLogs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(12)
            .Select(x => new AuditLogListItemViewModel
            {
                Id = x.Id,
                ActorUsername = x.ActorUsername,
                Action = x.Action,
                ActionLabel = GetActionLabel(x.Action),
                EntityType = x.EntityType,
                EntityKey = x.EntityKey,
                Note = x.Note,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var totalCount = await _context.AdminUsers.CountAsync();
        var activeCount = await _context.AdminUsers.CountAsync(x => x.IsActive);

        return new AdminUserIndexViewModel
        {
            Query = query?.Trim() ?? string.Empty,
            Role = normalizedRole,
            Status = normalizedStatus,
            Items = PagedListViewModel<AdminUserListItemViewModel>.Create(users, page),
            TotalCount = totalCount,
            ActiveCount = activeCount,
            LockedCount = totalCount - activeCount,
            AdminCount = await _context.AdminUsers.CountAsync(x => x.Role == AdminRoleNames.Admin),
            OwnerCount = await _context.AdminUsers.CountAsync(x => x.Role == AdminRoleNames.RestaurantOwner),
            RecentAuditLogs = recentLogs
        };
    }

    public async Task<AdminUserFormViewModel?> GetEditFormAsync(int id)
    {
        var user = await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
            return null;

        var ownedPoiCount = await _context.Pois
            .AsNoTracking()
            .CountAsync(x => x.OwnerAdminUserId == id);

        return new AdminUserFormViewModel
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            OwnedPoiCount = ownedPoiCount
        };
    }

    public Task<AdminUser?> GetDeleteModelAsync(int id)
    {
        return _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<AdminUserSaveResult> CreateAsync(AdminUserFormViewModel vm, string? currentUsername)
    {
        var normalizedUsername = NormalizeUsername(vm.Username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
            return AdminUserSaveResult.Fail("Vui lòng nhập tài khoản.");

        if (string.IsNullOrWhiteSpace(vm.Password))
            return AdminUserSaveResult.Fail("Vui lòng nhập mật khẩu cho tài khoản mới.");

        var duplicate = await _context.AdminUsers
            .AnyAsync(x => x.Username.ToLower() == normalizedUsername);

        if (duplicate)
            return AdminUserSaveResult.Fail("Tài khoản đã tồn tại.");

        var user = new AdminUser
        {
            Username = normalizedUsername,
            FullName = vm.FullName.Trim(),
            Role = NormalizeRole(vm.Role),
            PasswordHash = AdminPasswordHasher.Hash(vm.Password),
            IsActive = vm.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.AdminUsers.Add(user);
        await _context.SaveChangesAsync();

        await AddAuditLogAsync(
            actorUsername: currentUsername,
            action: "create_user",
            entityType: nameof(AdminUser),
            entityKey: user.Id.ToString(),
            note: $"Tạo tài khoản {user.Username} ({AdminRoleNames.DisplayName(user.Role)}).");

        return AdminUserSaveResult.Ok();
    }

    public async Task<AdminUserSaveResult> UpdateAsync(int id, AdminUserFormViewModel vm, string? currentUsername)
    {
        var user = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return AdminUserSaveResult.Fail("Không tìm thấy tài khoản.");

        var normalizedUsername = NormalizeUsername(vm.Username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
            return AdminUserSaveResult.Fail("Vui lòng nhập tài khoản.");

        var duplicate = await _context.AdminUsers
            .AnyAsync(x => x.Id != id && x.Username.ToLower() == normalizedUsername);

        if (duplicate)
            return AdminUserSaveResult.Fail("Tài khoản đã tồn tại.");

        var changes = new List<string>();

        if (!string.Equals(user.Username, normalizedUsername, StringComparison.Ordinal))
            changes.Add($"đổi tài khoản từ {user.Username} sang {normalizedUsername}");

        if (!string.Equals(user.FullName, vm.FullName.Trim(), StringComparison.Ordinal))
            changes.Add("cập nhật họ tên");

        var normalizedRole = NormalizeRole(vm.Role);
        if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
            changes.Add($"đổi quyền thành {AdminRoleNames.DisplayName(normalizedRole)}");

        if (user.IsActive != vm.IsActive)
            changes.Add(vm.IsActive ? "mở khóa tài khoản" : "khóa tài khoản");

        if (!string.IsNullOrWhiteSpace(vm.Password))
            changes.Add("đổi mật khẩu");

        user.Username = normalizedUsername;
        user.FullName = vm.FullName.Trim();
        user.Role = normalizedRole;
        user.IsActive = vm.IsActive;

        if (!string.IsNullOrWhiteSpace(vm.Password))
            user.PasswordHash = AdminPasswordHasher.Hash(vm.Password);

        await _context.SaveChangesAsync();

        await AddAuditLogAsync(
            actorUsername: currentUsername,
            action: "update_user",
            entityType: nameof(AdminUser),
            entityKey: user.Id.ToString(),
            note: changes.Count > 0
                ? $"Cập nhật tài khoản {user.Username}: {string.Join(", ", changes)}."
                : $"Lưu lại thông tin tài khoản {user.Username}."
        );

        return AdminUserSaveResult.Ok();
    }

    public async Task<AdminUserSaveResult> DeleteAsync(int id, string? currentUsername)
    {
        var user = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return AdminUserSaveResult.Fail("Không tìm thấy tài khoản.");

        if (string.Equals(user.Username, NormalizeUsername(currentUsername), StringComparison.OrdinalIgnoreCase))
            return AdminUserSaveResult.Fail("Không thể xóa tài khoản đang đăng nhập.");

        var deletedUsername = user.Username;
        var deletedRole = user.Role;

        _context.AdminUsers.Remove(user);
        await _context.SaveChangesAsync();

        await AddAuditLogAsync(
            actorUsername: currentUsername,
            action: "delete_user",
            entityType: nameof(AdminUser),
            entityKey: id.ToString(),
            note: $"Xóa tài khoản {deletedUsername} ({AdminRoleNames.DisplayName(deletedRole)}).");

        return AdminUserSaveResult.Ok();
    }

    public async Task<bool> ResetPasswordAsync(int id, string? currentUsername, string newPassword = "123456")
    {
        var user = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return false;

        user.PasswordHash = AdminPasswordHasher.Hash(newPassword);
        await _context.SaveChangesAsync();

        await AddAuditLogAsync(
            actorUsername: currentUsername,
            action: "reset_password",
            entityType: nameof(AdminUser),
            entityKey: user.Id.ToString(),
            note: $"Reset mật khẩu tài khoản {user.Username} về mặc định {newPassword}.");

        return true;
    }

    private async Task AddAuditLogAsync(string? actorUsername, string action, string entityType, string entityKey, string note)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            ActorUsername = NormalizeActorUsername(actorUsername),
            Action = action,
            EntityType = entityType,
            EntityKey = entityKey,
            Note = note,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private static string GetActionLabel(string? action)
        => action switch
        {
            "login_success" => "Đăng nhập",
            "create_user" => "Tạo tài khoản",
            "update_user" => "Cập nhật tài khoản",
            "reset_password" => "Reset mật khẩu",
            "delete_user" => "Xóa tài khoản",
            _ => "Thao tác"
        };

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

    private static string NormalizeSearch(string? query)
        => (query ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeRole(string? role)
        => AdminRoleNames.Normalize(role);

    private static string NormalizeRoleFilter(string? role)
    {
        var value = (role ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? string.Empty : AdminRoleNames.Normalize(value);
    }

    private static string NormalizeStatus(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "active" or "locked" ? value : string.Empty;
    }

    private static string NormalizeActorUsername(string? username)
    {
        var normalized = NormalizeUsername(username);
        return string.IsNullOrWhiteSpace(normalized) ? "system" : normalized;
    }
}
