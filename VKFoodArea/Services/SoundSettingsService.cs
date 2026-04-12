using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class SoundSettingsService
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;
    private readonly AppSettingsService _appSettingsService;
    private readonly AppLanguageService _appLanguageService;
    private readonly AppUserSyncService _appUserSyncService;

    public SoundSettingsService(
        AppDbContext db,
        AuthService authService,
        AppSettingsService appSettingsService,
        AppLanguageService appLanguageService,
        AppUserSyncService appUserSyncService)
    {
        _db = db;
        _authService = authService;
        _appSettingsService = appSettingsService;
        _appLanguageService = appLanguageService;
        _appUserSyncService = appUserSyncService;
    }

    public async Task<SoundSettingsSnapshot> GetCurrentSoundSettingsAsync(CancellationToken ct = default)
    {
        AppUser? user = null;
        var currentUserId = _authService.GetCurrentUserId();

        if (currentUserId.HasValue)
        {
            user = await _db.AppUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);
        }

        var language = AppLanguageService.NormalizeLanguage(
            user?.NarrationLanguage
            ?? _appSettingsService.NarrationLanguage
            ?? _appLanguageService.CurrentLanguage);
        var playbackMode = NormalizePlaybackMode(
            user?.NarrationPlaybackMode ?? _appSettingsService.NarrationOutputMode);

        return new SoundSettingsSnapshot(currentUserId, language, playbackMode);
    }

    public async Task<SoundSettingsUpdateResult> UpdateSoundSettingsAsync(
        string? language,
        string? playbackMode,
        CancellationToken ct = default)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        var normalizedPlaybackMode = NormalizePlaybackMode(playbackMode);

        var currentUserId = _authService.GetCurrentUserId();
        if (currentUserId.HasValue)
        {
            var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Id == currentUserId.Value, ct);
            if (user is null)
                return SoundSettingsUpdateResult.Fail("not_found");

            user.NarrationLanguage = normalizedLanguage;
            user.NarrationPlaybackMode = normalizedPlaybackMode;
            await _db.SaveChangesAsync(ct);

            if (_authService.CurrentUser?.Id == user.Id)
            {
                var clonedUser = CloneUser(user);
                _authService.ReplaceCurrentUser(clonedUser);
                await _appUserSyncService.SyncAsync(clonedUser, AuthService.BuildUserSyncKey(clonedUser), ct);
            }
        }

        ApplySoundSettings(normalizedLanguage, normalizedPlaybackMode);

        return SoundSettingsUpdateResult.Success(
            new SoundSettingsSnapshot(currentUserId, normalizedLanguage, normalizedPlaybackMode));
    }

    public void ApplySoundSettings(string? language, string? playbackMode)
    {
        var normalizedLanguage = AppLanguageService.NormalizeLanguage(language);
        var normalizedPlaybackMode = NormalizePlaybackMode(playbackMode);

        _appSettingsService.NarrationLanguage = normalizedLanguage;
        _appSettingsService.NarrationOutputMode = normalizedPlaybackMode;
        _appLanguageService.CurrentLanguage = normalizedLanguage;
    }

    public static string NormalizePlaybackMode(string? mode)
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

public sealed record SoundSettingsSnapshot(
    int? UserId,
    string Language,
    string PlaybackMode);

public sealed record SoundSettingsUpdateResult(
    bool IsSuccess,
    SoundSettingsSnapshot? Snapshot = null,
    string? ErrorCode = null)
{
    public static SoundSettingsUpdateResult Success(SoundSettingsSnapshot snapshot)
        => new(true, snapshot, null);

    public static SoundSettingsUpdateResult Fail(string errorCode)
        => new(false, null, errorCode);
}
