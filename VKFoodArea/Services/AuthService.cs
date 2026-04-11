using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly SessionStoreService _sessionStore;
    private readonly AppSettingsService _settingsService;
    private readonly AppLanguageService _languageService;
    private readonly AppUserSyncService _appUserSyncService;

    public AppUser? CurrentUser { get; private set; }

    public AuthService(
        AppDbContext db,
        SessionStoreService sessionStore,
        AppSettingsService settingsService,
        AppLanguageService languageService,
        AppUserSyncService appUserSyncService)
    {
        _db = db;
        _sessionStore = sessionStore;
        _settingsService = settingsService;
        _languageService = languageService;
        _appUserSyncService = appUserSyncService;
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

        CurrentUser = user;
        ApplyUserSoundSettings(user);
        _sessionStore.Save(user.Id);
        await _appUserSyncService.SyncAsync(user, userKey);
        return AuthActionResult.Success(user);
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

        var emailExists = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Email == normalizedEmail);

        if (emailExists)
            return AuthActionResult.Fail("Register.DuplicateEmailError");

        var username = await GenerateUsernameAsync(normalizedEmail);

        var user = new AppUser
        {
            FullName = normalizedFullName,
            Email = normalizedEmail,
            Username = username,
            PasswordHash = HashPassword(password),
            NarrationLanguage = "vi",
            NarrationPlaybackMode = "TTS",
            Role = "User",
            IsActive = true
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();
        await _appUserSyncService.SyncAsync(user, BuildUserSyncKey(user));

        return AuthActionResult.Success(user);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var userId = _sessionStore.GetCurrentUserId();
        if (!userId.HasValue)
            return false;

        var user = await _db.AppUsers
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
            _sessionStore.Clear();
            CurrentUser = null;
            return false;
        }

        CurrentUser = user;
        ApplyUserSoundSettings(user);
        await _appUserSyncService.SyncAsync(user, userKey);
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
        => BuildUserSyncKey(CurrentUser);

    public static string? BuildUserSyncKey(AppUser? user)
    {
        var identifier = user?.Email;

        if (string.IsNullOrWhiteSpace(identifier))
            identifier = user?.Username;

        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(identifier.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }

    public void ReplaceCurrentUser(AppUser? user)
    {
        CurrentUser = user;

        if (user is not null)
            ApplyUserSoundSettings(user);
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
            var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);
            if (user is not null)
            {
                user.NarrationLanguage = normalizedLanguage;
                user.NarrationPlaybackMode = normalizedPlaybackMode;
                await _db.SaveChangesAsync(ct);
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

        return await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                (x.Email == normalizedIdentifier || x.Username.ToLower() == normalizedIdentifier));
    }

    private async Task<string> GenerateUsernameAsync(string email)
    {
        var seed = email.Split('@', 2)[0];
        var baseUsername = SanitizeUsername(seed);
        var username = baseUsername;
        var suffix = 2;

        while (await _db.AppUsers.AsNoTracking().AnyAsync(x => x.Username == username))
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
            Role = user.Role,
            IsActive = user.IsActive
        };
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}

public sealed record AuthActionResult(bool IsSuccess, AppUser? User = null, string? ErrorKey = null)
{
    public static AuthActionResult Success(AppUser user) => new(true, user, null);

    public static AuthActionResult Fail(string errorKey) => new(false, null, errorKey);
}
