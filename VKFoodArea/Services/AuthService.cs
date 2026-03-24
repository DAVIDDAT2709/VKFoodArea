using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AppUser? CurrentUser { get; private set; }

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var hash = HashPassword(password);

        var user = await _db.AppUsers.FirstOrDefaultAsync(x =>
            x.Username == username &&
            x.PasswordHash == hash &&
            x.IsActive);

        if (user is null)
            return false;

        CurrentUser = user;
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
    }
    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}