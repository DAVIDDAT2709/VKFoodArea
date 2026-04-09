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

    public AppUser? CurrentUser { get; private set; }

    public AuthService(AppDbContext db, SessionStoreService sessionStore)
    {
        _db = db;
        _sessionStore = sessionStore;
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

        CurrentUser = user;
        _sessionStore.Save(user.Id);
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
            Role = "User",
            IsActive = true
        };

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

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

        CurrentUser = user;
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
        _sessionStore.Clear();
    }

    public int? GetCurrentUserId()
        => CurrentUser?.Id ?? _sessionStore.GetCurrentUserId();

    public void ReplaceCurrentUser(AppUser? user)
    {
        CurrentUser = user;
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
