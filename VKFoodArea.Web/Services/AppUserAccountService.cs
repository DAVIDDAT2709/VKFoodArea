using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class AppUserAccountService : IAppUserAccountService
{
    private readonly AppDbContext _context;

    public AppUserAccountService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppUserAccountListItemViewModel>> GetAllAsync()
    {
        var users = await _context.AppUserAccounts
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeenAt)
            .ThenBy(x => x.Username)
            .ToListAsync();

        var userKeys = users
            .Select(x => x.UserKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var historyStats = await _context.NarrationHistories
            .AsNoTracking()
            .Where(x => userKeys.Contains(x.UserKey))
            .GroupBy(x => x.UserKey)
            .Select(x => new
            {
                UserKey = x.Key,
                ListenCount = x.Count(),
                LatestPlayedAt = x.Max(item => (DateTime?)item.PlayedAt)
            })
            .ToDictionaryAsync(x => x.UserKey);

        return users
            .Select(user =>
            {
                historyStats.TryGetValue(user.UserKey, out var stats);
                return ToListItem(user, stats?.ListenCount ?? 0, stats?.LatestPlayedAt);
            })
            .ToList();
    }

    public async Task<AppUserAccountDetailsViewModel?> GetDetailsAsync(int id)
    {
        var user = (await GetAllAsync()).FirstOrDefault(x => x.Id == id);
        if (user is null)
            return null;

        var recentNarrations = await _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.UserKey == user.UserKey)
            .OrderByDescending(x => x.PlayedAt)
            .Take(30)
            .ToListAsync();

        return new AppUserAccountDetailsViewModel
        {
            User = user,
            RecentNarrations = recentNarrations
        };
    }

    public async Task<AppUserAccountListItemViewModel> SyncFromAppAsync(AppUserAccountSyncViewModel vm)
    {
        var now = DateTime.UtcNow;
        var userKey = NormalizeUserKey(vm.UserKey);
        if (string.IsNullOrWhiteSpace(userKey))
            throw new InvalidOperationException("Missing user key.");

        var normalizedRole = AppUserRoleNames.Normalize(vm.Role);
        var user = await _context.AppUserAccounts.FirstOrDefaultAsync(x => x.UserKey == userKey);
        if (user is null)
        {
            user = new AppUserAccount
            {
                UserKey = userKey,
                CreatedAt = now,
                Role = normalizedRole == AppUserRoleNames.Guest ? AppUserRoleNames.User : normalizedRole,
                IsActive = vm.IsActive
            };
            _context.AppUserAccounts.Add(user);
        }

        user.Username = NormalizeText(vm.Username);
        user.Email = NormalizeText(vm.Email).ToLowerInvariant();
        user.FullName = NormalizeText(vm.FullName);
        user.NarrationLanguage = NormalizeLanguage(vm.NarrationLanguage);
        user.NarrationPlaybackMode = NormalizePlaybackMode(vm.NarrationPlaybackMode);
        user.LastSeenAt = now;
        user.LastSyncedAt = now;

        await _context.SaveChangesAsync();
        return ToListItem(user, listenCount: 0, latestPlayedAt: null);
    }

    public async Task<AppUserAccountStatusViewModel> GetStatusAsync(string? userKey)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
        {
            return new AppUserAccountStatusViewModel
            {
                IsKnown = false,
                Role = AppUserRoleNames.Guest,
                IsActive = true
            };
        }

        var user = await _context.AppUserAccounts
            .AsNoTracking()
            .Where(x => x.UserKey == normalizedUserKey)
            .Select(x => new { x.UserKey, x.Role, x.IsActive })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return new AppUserAccountStatusViewModel
            {
                UserKey = normalizedUserKey,
                IsKnown = false,
                Role = AppUserRoleNames.Guest,
                IsActive = true
            };
        }

        return new AppUserAccountStatusViewModel
        {
            UserKey = user.UserKey,
            IsKnown = true,
            Role = AppUserRoleNames.Normalize(user.Role),
            IsActive = user.IsActive
        };
    }

    public async Task<bool> UpdateAccessAsync(int id, string role, bool isActive, string? actorUsername)
    {
        var user = await _context.AppUserAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return false;

        var normalizedRole = AppUserRoleNames.Normalize(role);
        if (normalizedRole == AppUserRoleNames.Guest)
            normalizedRole = AppUserRoleNames.User;

        var changes = new List<string>();
        if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
            changes.Add($"doi quyen thanh {AppUserRoleNames.DisplayName(normalizedRole)}");

        if (user.IsActive != isActive)
            changes.Add(isActive ? "mo khoa tai khoan app" : "khoa tai khoan app");

        user.Role = normalizedRole;
        user.IsActive = isActive;
        await _context.SaveChangesAsync();

        _context.AuditLogs.Add(new AuditLog
        {
            ActorUsername = NormalizeActorUsername(actorUsername),
            Action = "update_app_user_access",
            EntityType = nameof(AppUserAccount),
            EntityKey = user.Id.ToString(),
            Note = changes.Count > 0
                ? $"Cap nhat app user {BuildUserLabel(user)}: {string.Join(", ", changes)}."
                : $"Luu lai quyen app user {BuildUserLabel(user)}.",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    private static AppUserAccountListItemViewModel ToListItem(
        AppUserAccount user,
        int listenCount,
        DateTime? latestPlayedAt)
        => new()
        {
            Id = user.Id,
            UserKey = user.UserKey,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            NarrationLanguage = user.NarrationLanguage,
            NarrationPlaybackMode = user.NarrationPlaybackMode,
            Role = AppUserRoleNames.Normalize(user.Role),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastSeenAt = user.LastSeenAt,
            LastSyncedAt = user.LastSyncedAt,
            ListenCount = listenCount,
            LatestPlayedAt = latestPlayedAt
        };

    private static string NormalizeUserKey(string? userKey)
        => NormalizeText(userKey).ToLowerInvariant();

    private static string NormalizeText(string? text)
        => (text ?? string.Empty).Trim();

    private static string NormalizeLanguage(string? language)
    {
        return NormalizeText(language).ToLowerInvariant() switch
        {
            "en" => "en",
            "zh" => "zh",
            "ja" => "ja",
            "de" => "de",
            _ => "vi"
        };
    }

    private static string NormalizePlaybackMode(string? mode)
    {
        return NormalizeText(mode) switch
        {
            "Auto" => "Auto",
            "Audio" => "Audio",
            _ => "TTS"
        };
    }

    private static string NormalizeActorUsername(string? username)
    {
        var normalized = NormalizeText(username).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "system" : normalized;
    }

    private static string BuildUserLabel(AppUserAccount user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username))
            return user.Username;

        if (!string.IsNullOrWhiteSpace(user.Email))
            return user.Email;

        return user.UserKey;
    }
}
