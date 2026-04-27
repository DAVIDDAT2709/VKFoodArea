using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AuthService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly SessionStoreService _sessionStore;
    private readonly AppSettingsService _settingsService;
    private readonly AppLanguageService _languageService;
    private readonly AppUserSyncService _appUserSyncService;
    private readonly AnonymousIdentityService _anonymousIdentityService;

    public AppUser? CurrentUser { get; private set; }

    public AuthService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        SessionStoreService sessionStore,
        AppSettingsService settingsService,
        AppLanguageService languageService,
        AppUserSyncService appUserSyncService,
        AnonymousIdentityService anonymousIdentityService)
    {
        _dbContextFactory = dbContextFactory;
        _sessionStore = sessionStore;
        _settingsService = settingsService;
        _languageService = languageService;
        _appUserSyncService = appUserSyncService;
        _anonymousIdentityService = anonymousIdentityService;
    }

    public async Task<AuthActionResult> LoginAsync(string identifier, string password)
    {
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        var user = await FindUserAsync(normalizedIdentifier);

        if (user is null)
            return AuthActionResult.Fail("Login.InvalidError");

        if (!VerifyPassword(password, user.PasswordHash))
            return AuthActionResult.Fail("Login.InvalidError");

        if (!user.IsActive)
            return AuthActionResult.Fail("Login.DisabledError");

        var userKey = BuildUserSyncKey(user);
        var remoteStatus = await _appUserSyncService.GetStatusAsync(userKey);
        if (remoteStatus is { IsKnown: true, IsActive: false })
            return AuthActionResult.Fail("Login.DisabledError");

        CurrentUser = await ApplyRemoteAccessAsync(user, remoteStatus);
        ApplyUserSoundSettings(CurrentUser);
        _sessionStore.Save(CurrentUser.Id);
        await _appUserSyncService.SyncAsync(CurrentUser, userKey);
        return AuthActionResult.Success(CurrentUser);
    }

    public async Task<AuthActionResult> RegisterAsync(string fullName, string email, string password)
    {
        var normalizedFullName = (fullName ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalizedFullName) ||
            string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(password))
        {
            return AuthActionResult.Fail("Register.RequiredError");
        }

        if (!LooksLikeEmail(normalizedEmail))
            return AuthActionResult.Fail("Register.InvalidEmailError");

        if (password.Length < 6)
            return AuthActionResult.Fail("Register.PasswordTooShortError");

        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var emailExists = await db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Email == normalizedEmail);

        if (emailExists)
            return AuthActionResult.Fail("Register.DuplicateEmailError");

        var username = await GenerateUsernameAsync(db, normalizedEmail);

        var user = new AppUser
        {
            FullName = normalizedFullName,
            Email = normalizedEmail,
            Username = username,
            PasswordHash = HashPassword(password),
            NarrationLanguage = "vi",
            NarrationPlaybackMode = "TTS",
            Role = AppUserRoleNames.User,
            IsActive = true
        };

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        await _appUserSyncService.SyncAsync(user, BuildUserSyncKey(user));

        return AuthActionResult.Success(CloneUser(user));
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var userId = _sessionStore.GetCurrentUserId();
        if (!userId.HasValue)
            return false;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var user = await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value && x.IsActive);

        if (user is null)
        {
            _sessionStore.Clear();
            CurrentUser = null;
            return false;
        }

        var userKey = BuildUserSyncKey(user);
        var remoteStatus = await _appUserSyncService.GetStatusAsync(userKey);
        if (remoteStatus is { IsKnown: true, IsActive: false })
        {
            await MarkUserActiveStateAsync(user.Id, false);
            _sessionStore.Clear();
            CurrentUser = null;
            return false;
        }

        CurrentUser = await ApplyRemoteAccessAsync(user, remoteStatus);
        ApplyUserSoundSettings(CurrentUser);
        await _appUserSyncService.SyncAsync(CurrentUser, userKey);
        return true;
    }

    public async Task<bool> RefreshCurrentUserAccessAsync(CancellationToken ct = default)
    {
        if (CurrentUser is null)
            return false;

        var userKey = BuildUserSyncKey(CurrentUser);
        var remoteStatus = await _appUserSyncService.GetStatusAsync(userKey, ct);
        if (remoteStatus is null || !remoteStatus.IsKnown)
            return false;

        if (!remoteStatus.IsActive)
        {
            await MarkUserActiveStateAsync(CurrentUser.Id, false, ct);
            Logout();
            return true;
        }

        CurrentUser = await ApplyRemoteAccessAsync(CurrentUser, remoteStatus, ct);
        ApplyUserSoundSettings(CurrentUser);
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
        _sessionStore.Clear();
    }

    public int? GetCurrentUserId()
        => CurrentUser?.Id ?? _sessionStore.GetCurrentUserId();

    public string? GetCurrentUserSyncKey()
    {
        var currentUserKey = BuildUserSyncKey(CurrentUser);
        return string.IsNullOrWhiteSpace(currentUserKey)
            ? _anonymousIdentityService.GetOrCreateAnonymousUserKey()
            : currentUserKey;
    }

    public static string? BuildUserSyncKey(AppUser? user)
    {
        var identifier = user?.Email;

        if (string.IsNullOrWhiteSpace(identifier))
            identifier = user?.Username;

        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }

    public void ReplaceCurrentUser(AppUser? user)
    {
        CurrentUser = user is null ? null : CloneUser(user);

        if (CurrentUser is not null)
            ApplyUserSoundSettings(CurrentUser);
    }

    public async Task UpdateCurrentUserSoundSettingsAsync(
        string? language,
        string? playbackMode = null,
        CancellationToken ct = default)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        var normalizedPlaybackMode = SoundSettingsService.NormalizePlaybackMode(
            playbackMode ?? _settingsService.NarrationOutputMode);

        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            var user = await db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);
            if (user is not null)
            {
                user.NarrationLanguage = normalizedLanguage;
                user.NarrationPlaybackMode = normalizedPlaybackMode;
                await db.SaveChangesAsync(ct);
                CurrentUser = CloneUser(user);
                await _appUserSyncService.SyncAsync(CurrentUser, BuildUserSyncKey(CurrentUser), ct);
            }
        }

        _settingsService.NarrationLanguage = normalizedLanguage;
        _settingsService.NarrationOutputMode = normalizedPlaybackMode;
        _languageService.CurrentLanguage = normalizedLanguage;
    }

    private void ApplyUserSoundSettings(AppUser user)
    {
        var language = AppLanguageService.NormalizeLanguage(user.NarrationLanguage);
        var playbackMode = SoundSettingsService.NormalizePlaybackMode(user.NarrationPlaybackMode);

        _settingsService.NarrationLanguage = language;
        _settingsService.NarrationOutputMode = playbackMode;
        _languageService.CurrentLanguage = language;
    }

    private async Task<AppUser?> FindUserAsync(string normalizedIdentifier)
    {
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            return null;

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.Email == normalizedIdentifier || x.Username.ToLower() == normalizedIdentifier));
    }

    private async Task<AppUser> ApplyRemoteAccessAsync(
        AppUser user,
        AppUserStatusDto? remoteStatus,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var normalizedRole = remoteStatus is { IsKnown: true }
            ? AppUserRoleNames.Normalize(remoteStatus.Role)
            : AppUserRoleNames.Normalize(user.Role);
        var isActive = remoteStatus is { IsKnown: true }
            ? remoteStatus.IsActive
            : user.IsActive;

        var trackedUser = await db.AppUsers.FirstOrDefaultAsync(x => x.Id == user.Id, ct);
        if (trackedUser is null)
        {
            var updatedUser = CloneUser(user);
            updatedUser.Role = normalizedRole;
            updatedUser.IsActive = isActive;
            return updatedUser;
        }

        var hasChanges = false;
        if (!string.Equals(trackedUser.Role, normalizedRole, StringComparison.Ordinal))
        {
            trackedUser.Role = normalizedRole;
            hasChanges = true;
        }

        if (trackedUser.IsActive != isActive)
        {
            trackedUser.IsActive = isActive;
            hasChanges = true;
        }

        if (hasChanges)
            await db.SaveChangesAsync(ct);

        return CloneUser(trackedUser);
    }

    private async Task MarkUserActiveStateAsync(int userId, bool isActive, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var trackedUser = await db.AppUsers.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (trackedUser is null || trackedUser.IsActive == isActive)
            return;

        trackedUser.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<string> GenerateUsernameAsync(AppDbContext db, string email)
    {
        var seed = email.Split('@', 2)[0];
        var baseUsername = SanitizeUsername(seed);
        var username = baseUsername;
        var suffix = 2;

        while (await db.AppUsers.AsNoTracking().AnyAsync(x => x.Username == username))
        {
            username = $"{baseUsername}{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return username;
    }

    private static string SanitizeUsername(string? raw)
    {
        var builder = new StringBuilder();

        foreach (var character in (raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
                builder.Append(character);
        }

        if (builder.Length >= 4)
            return builder.ToString();

        if (builder.Length == 0)
            return "user";

        return builder.Append("user").ToString();
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeIdentifier(string? identifier)
        => (identifier ?? string.Empty).Trim().ToLowerInvariant();

    private static bool LooksLikeEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static bool VerifyPassword(string password, string passwordHash)
        => string.Equals(HashPassword(password), passwordHash, StringComparison.Ordinal);

    private static AppUser CloneUser(AppUser user)
    {
        return new AppUser
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            FullName = user.FullName,
            NarrationLanguage = user.NarrationLanguage,
            NarrationPlaybackMode = user.NarrationPlaybackMode,
            Role = AppUserRoleNames.Normalize(user.Role),
            IsActive = user.IsActive
        };
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}

public sealed record AuthActionResult(bool IsSuccess, AppUser? User = null, string? ErrorKey = null)
{
    public static AuthActionResult Success(AppUser user) => new(true, user, null);

    public static AuthActionResult Fail(string errorKey) => new(false, null, errorKey);
}
