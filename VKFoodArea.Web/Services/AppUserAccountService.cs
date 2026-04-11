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

        var user = await _context.AppUserAccounts.FirstOrDefaultAsync(x => x.UserKey == userKey);
        if (user is null)
        {
            user = new AppUserAccount
            {
                UserKey = userKey,
                CreatedAt = now,
                IsActive = vm.IsActive
            };
            _context.AppUserAccounts.Add(user);
        }

        user.Username = NormalizeText(vm.Username);
        user.Email = NormalizeText(vm.Email).ToLowerInvariant();
        user.FullName = NormalizeText(vm.FullName);
        user.NarrationLanguage = NormalizeText(vm.NarrationLanguage).ToLowerInvariant();
        user.NarrationPlaybackMode = NormalizePlaybackMode(vm.NarrationPlaybackMode);
        user.Role = string.IsNullOrWhiteSpace(vm.Role) ? "User" : NormalizeText(vm.Role);
        user.LastSeenAt = now;
        user.LastSyncedAt = now;

        await _context.SaveChangesAsync();
        return ToListItem(user, listenCount: 0, latestPlayedAt: null);
    }

    public async Task<AppUserAccountStatusViewModel> GetStatusAsync(string? userKey)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
            return new AppUserAccountStatusViewModel { IsKnown = false, IsActive = true };

        var user = await _context.AppUserAccounts
            .AsNoTracking()
            .Where(x => x.UserKey == normalizedUserKey)
            .Select(x => new { x.UserKey, x.IsActive })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return new AppUserAccountStatusViewModel
            {
                UserKey = normalizedUserKey,
                IsKnown = false,
                IsActive = true
            };
        }

        return new AppUserAccountStatusViewModel
        {
            UserKey = user.UserKey,
            IsKnown = true,
            IsActive = user.IsActive
        };
    }

    public async Task<bool> SetActiveAsync(int id, bool isActive)
    {
        var user = await _context.AppUserAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
            return false;

        user.IsActive = isActive;
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
            Role = user.Role,
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

    private static string NormalizePlaybackMode(string? mode)
    {
        return NormalizeText(mode) switch
        {
            "Auto" => "Auto",
            "Audio" => "Audio",
            _ => "TTS"
        };
    }

}
