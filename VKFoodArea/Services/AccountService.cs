using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AccountService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AppSettingsService _settingsService;
    private readonly AppLanguageService _languageService;

    public AccountService(
        AppDbContext db,
        AuthService authService,
        AppSettingsService settingsService,
        AppLanguageService languageService)
    {
        _db = db;
        _authService = authService;
        _settingsService = settingsService;
        _languageService = languageService;
    }

    public async Task<AccountSettingsSnapshot?> GetUserProfileAsync(CancellationToken ct = default)
    {
        var currentUserId = _authService.GetCurrentUserId();
        if (!currentUserId.HasValue)
            return null;

        var user = await _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);

        return user is null ? null : BuildSnapshot(user);
    }

    public async Task<AccountServiceResult> UpdateUserProfileAsync(
        AccountProfileUpdateRequest request,
        CancellationToken ct = default)
    {
        var currentUserId = _authService.GetCurrentUserId();
        if (!currentUserId.HasValue)
            return AccountServiceResult.Fail("not_found");

        var normalizedFullName = (request.FullName ?? string.Empty).Trim();
        var normalizedEmail = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(normalizedFullName) ||
            string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return AccountServiceResult.Fail("required");
        }

        if (!LooksLikeEmail(normalizedEmail))
            return AccountServiceResult.Fail("invalid_email");

        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);
        if (user is null)
            return AccountServiceResult.Fail("not_found");

        var emailInUse = await _db.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id != user.Id && x.Email == normalizedEmail, ct);

        if (emailInUse)
            return AccountServiceResult.Fail("duplicate_email");

        user.FullName = normalizedFullName;
        user.Email = normalizedEmail;

        var normalizedLanguage = AppLanguageService.NormalizeLanguage(request.Language);
        var normalizedPlaybackMode = NormalizePlaybackMode(request.PlaybackMode);

        user.NarrationLanguage = normalizedLanguage;
        user.NarrationPlaybackMode = normalizedPlaybackMode;
        await _db.SaveChangesAsync(ct);

        _settingsService.NarrationLanguage = normalizedLanguage;
        _settingsService.NarrationOutputMode = normalizedPlaybackMode;
        _languageService.CurrentLanguage = normalizedLanguage;
        _authService.ReplaceCurrentUser(CloneUser(user));

        return AccountServiceResult.Success(BuildSnapshot(user));
    }

    private AccountSettingsSnapshot BuildSnapshot(AppUser user)
    {
        return new AccountSettingsSnapshot(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            AppLanguageService.NormalizeLanguage(user.NarrationLanguage ?? _settingsService.NarrationLanguage),
            NormalizePlaybackMode(user.NarrationPlaybackMode ?? _settingsService.NarrationOutputMode));
    }

    private static string NormalizeEmail(string? email)
        => (email ?? string.Empty).Trim().ToLowerInvariant();

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

    private static string NormalizePlaybackMode(string? mode)
    {
        return (mode ?? "TTS").Trim() switch
        {
            "Auto" => "Auto",
            "Audio" => "Audio",
            _ => "TTS"
        };
    }

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
}

public sealed record AccountSettingsSnapshot(
    int UserId,
    string Username,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string Language,
    string PlaybackMode);

public sealed record AccountProfileUpdateRequest(
    string FullName,
    string Email,
    string Language,
    string PlaybackMode);

public sealed record AccountServiceResult(
    bool IsSuccess,
    AccountSettingsSnapshot? Snapshot = null,
    string? ErrorCode = null)
{
    public static AccountServiceResult Success(AccountSettingsSnapshot snapshot)
        => new(true, snapshot, null);

    public static AccountServiceResult Fail(string errorCode)
        => new(false, null, errorCode);
}
