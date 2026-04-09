using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using VKFoodArea.Models;

namespace VKFoodArea.Data;

public static class AppDataInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
#if DEBUG
        if (string.Equals(Environment.GetEnvironmentVariable("VKFOODAREA_RESET_DB"), "1", StringComparison.Ordinal))
            await db.Database.EnsureDeletedAsync();
#endif

        await db.Database.EnsureCreatedAsync();
        await EnsureAppUsersEmailColumnAsync(db);
        await SeedMissingEmailsAsync(db);

        if (!await db.AppUsers.AnyAsync())
        {
            db.AppUsers.Add(
                new AppUser
                {
                    Username = "user",
                    Email = "user@vkfoodarea.local",
                    PasswordHash = HashPassword("123456"),
                    FullName = "Người dùng demo",
                    Role = "User",
                    IsActive = true
                });
        }

        if (!await db.FoodItems.AnyAsync())
        {
            db.FoodItems.AddRange(
                new FoodItem
                {
                    Name = "Ốc hương rang muối ớt",
                    RestaurantName = "Ốc Oanh",
                    Price = 89000,
                    ImageUrl = "ochuongrangmuoi.jpg",
                    Category = "Recommended",
                    DisplayOrder = 1
                },
                new FoodItem
                {
                    Name = "Sò điệp nướng mỡ hành",
                    RestaurantName = "Ốc Oanh",
                    Price = 79000,
                    ImageUrl = "sodiepnuongmohanh.jpg",
                    Category = "Recommended",
                    DisplayOrder = 2
                },
                new FoodItem
                {
                    Name = "Ốc xào me",
                    RestaurantName = "Ốc Vũ",
                    Price = 65000,
                    ImageUrl = "ocxaome.jpg",
                    Category = "Recommended",
                    DisplayOrder = 3
                },
                new FoodItem
                {
                    Name = "Nghêu hấp sả",
                    RestaurantName = "Ốc Thảo Quận 4",
                    Price = 59000,
                    ImageUrl = "ngheuhapsa.jpg",
                    Category = "Recommended",
                    DisplayOrder = 4
                },
                new FoodItem
                {
                    Name = "Ốc len xào dừa",
                    RestaurantName = "Ốc Bụi",
                    Price = 69000,
                    ImageUrl = "oclenxaodua.jpg",
                    Category = "Recommended",
                    DisplayOrder = 5
                },
                new FoodItem
                {
                    Name = "Gà Tiềm Ớt Xiêm",
                    RestaurantName = "Ớt Xiêm Quán",
                    Price = 199000,
                    ImageUrl = "gatiemotxiem.jpg",
                    Category = "StreetFood",
                    DisplayOrder = 1
                },
                new FoodItem
                {
                    Name = "Bao Tử Hầm tiêu",
                    RestaurantName = "Ớt Xiêm Quán",
                    Price = 99000,
                    ImageUrl = "baotuhamtieu.jpg",
                    Category = "StreetFood",
                    DisplayOrder = 2
                },
                new FoodItem
                {
                    Name = "Ốc luộc",
                    RestaurantName = "Ốc Nhi",
                    Price = 29000,
                    ImageUrl = "ocluoc.jpg",
                    Category = "StreetFood",
                    DisplayOrder = 3
                },
                new FoodItem
                {
                    Name = "Mực nướng",
                    RestaurantName = "Ốc Loan",
                    Price = 85000,
                    ImageUrl = "mucnuong.jpg",
                    Category = "StreetFood",
                    DisplayOrder = 4
                },
                new FoodItem
                {
                    Name = "Sườn muối ớt",
                    RestaurantName = "Sườn Muối Ớt Q4",
                    Price = 99000,
                    ImageUrl = "suon_muoi_ot.jpg",
                    Category = "StreetFood",
                    DisplayOrder = 5
                });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureAppUsersEmailColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (await HasColumnAsync(connection, "AppUsers", "Email"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE AppUsers ADD COLUMN Email TEXT NOT NULL DEFAULT '';");
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task SeedMissingEmailsAsync(AppDbContext db)
    {
        var users = await db.AppUsers
            .Where(x => string.IsNullOrWhiteSpace(x.Email))
            .ToListAsync();

        if (users.Count == 0)
            return;

        foreach (var user in users)
        {
            var username = string.IsNullOrWhiteSpace(user.Username)
                ? $"user{user.Id}"
                : user.Username.Trim().ToLowerInvariant();

            user.Email = $"{username}@vkfoodarea.local";
        }

        await db.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
