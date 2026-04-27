using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Data;

public static class AppDataInitializer
{
    public static async Task InitializeAsync(AppDbContext db, bool internalToolsEnabled = false)
    {
#if DEBUG
        if (string.Equals(Environment.GetEnvironmentVariable("VKFOODAREA_RESET_DB"), "1", StringComparison.Ordinal))
            await db.Database.EnsureDeletedAsync();
#endif

        await db.Database.EnsureCreatedAsync();
        await EnsureAppUsersEmailColumnAsync(db);
        await EnsureAppUsersSoundSettingsColumnsAsync(db);
        await EnsureAppUsersRoleColumnAsync(db);
        await EnsurePoiAudioColumnsAsync(db);
        await EnsurePoiMapUrlColumnAsync(db);
        await EnsureNarrationLogsUserColumnAsync(db);
        await EnsureNarrationLogsTourContextColumnsAsync(db);
        await EnsureAppSyncOutboxTableAsync(db);
        await SeedMissingEmailsAsync(db);
        await SeedMissingSoundSettingsAsync(db);
        await SeedMissingAppUserRolesAsync(db, internalToolsEnabled);
        await SeedOrRefreshPoisAsync(db);

        if (!await db.AppUsers.AnyAsync())
        {
            db.AppUsers.Add(
                new AppUser
                {
                    Username = "user",
                    Email = "user@vkfoodarea.local",
                    PasswordHash = HashPassword("123456"),
                    FullName = "Người dùng demo",
                    NarrationLanguage = "vi",
                    NarrationPlaybackMode = "TTS",
                    Role = internalToolsEnabled ? AppUserRoleNames.Operator : AppUserRoleNames.User,
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

    private static async Task EnsureAppUsersSoundSettingsColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "AppUsers", "NarrationLanguage"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AppUsers ADD COLUMN NarrationLanguage TEXT NOT NULL DEFAULT 'vi';");
        }

        if (!await HasColumnAsync(connection, "AppUsers", "NarrationPlaybackMode"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AppUsers ADD COLUMN NarrationPlaybackMode TEXT NOT NULL DEFAULT 'TTS';");
        }
    }

    private static async Task EnsureAppUsersRoleColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (await HasColumnAsync(connection, "AppUsers", "Role"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            $"ALTER TABLE AppUsers ADD COLUMN Role TEXT NOT NULL DEFAULT '{AppUserRoleNames.User}';");
    }

    private static async Task EnsureNarrationLogsUserColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (await HasColumnAsync(connection, "NarrationLogs", "UserId"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE NarrationLogs ADD COLUMN UserId INTEGER NULL;");
    }

    private static async Task EnsureNarrationLogsTourContextColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "NarrationLogs", "TourId"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NarrationLogs ADD COLUMN TourId INTEGER NULL;");
        }

        if (!await HasColumnAsync(connection, "NarrationLogs", "TourName"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NarrationLogs ADD COLUMN TourName TEXT NOT NULL DEFAULT '';");
        }

        if (!await HasColumnAsync(connection, "NarrationLogs", "TriggerSource"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NarrationLogs ADD COLUMN TriggerSource TEXT NOT NULL DEFAULT 'manual';");
        }
    }

    private static async Task EnsurePoiAudioColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "Pois", "AudioFileVi"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Pois ADD COLUMN AudioFileVi TEXT NOT NULL DEFAULT '';");
        }

        if (!await HasColumnAsync(connection, "Pois", "AudioFileEn"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Pois ADD COLUMN AudioFileEn TEXT NOT NULL DEFAULT '';");
        }

        if (!await HasColumnAsync(connection, "Pois", "AudioFileJa"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Pois ADD COLUMN AudioFileJa TEXT NOT NULL DEFAULT '';");
        }
    }

    private static async Task EnsurePoiMapUrlColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (await HasColumnAsync(connection, "Pois", "MapUrl"))
            return;

        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE Pois ADD COLUMN MapUrl TEXT NOT NULL DEFAULT '';");
    }

    private static async Task EnsureAppSyncOutboxTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS AppSyncOutboxItems (
                Id INTEGER NOT NULL CONSTRAINT PK_AppSyncOutboxItems PRIMARY KEY AUTOINCREMENT,
                SyncType TEXT NOT NULL DEFAULT '',
                RelativePath TEXT NOT NULL DEFAULT '',
                PayloadJson TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                NextRetryAt TEXT NOT NULL,
                LastAttemptAt TEXT NULL,
                AttemptCount INTEGER NOT NULL DEFAULT 0,
                LastError TEXT NOT NULL DEFAULT '',
                DiscardedAt TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_AppSyncOutboxItems_NextRetryAt ON AppSyncOutboxItems(NextRetryAt);");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_AppSyncOutboxItems_DiscardedAt ON AppSyncOutboxItems(DiscardedAt);");
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

    private static async Task SeedMissingSoundSettingsAsync(AppDbContext db)
    {
        var users = await db.AppUsers.ToListAsync();
        var hasChanges = false;

        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.NarrationLanguage))
            {
                user.NarrationLanguage = "vi";
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(user.NarrationPlaybackMode))
            {
                user.NarrationPlaybackMode = "TTS";
                hasChanges = true;
            }
        }

        if (hasChanges)
            await db.SaveChangesAsync();
    }

    private static async Task SeedMissingAppUserRolesAsync(AppDbContext db, bool internalToolsEnabled)
    {
        var users = await db.AppUsers.ToListAsync();
        var hasChanges = false;

        foreach (var user in users)
        {
            var normalizedRole = AppUserRoleNames.Normalize(user.Role);
            if (!string.Equals(user.Role, normalizedRole, StringComparison.Ordinal))
            {
                user.Role = normalizedRole;
                hasChanges = true;
            }

            if (internalToolsEnabled &&
                string.Equals(user.Email, "user@vkfoodarea.local", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(user.Role, AppUserRoleNames.User, StringComparison.Ordinal))
            {
                user.Role = AppUserRoleNames.Operator;
                hasChanges = true;
            }
        }

        if (hasChanges)
            await db.SaveChangesAsync();
    }

    private static async Task SeedOrRefreshPoisAsync(AppDbContext db)
    {
        var existingPois = await db.Pois.ToListAsync();

        if (existingPois.Count == 0)
        {
            db.Pois.AddRange(SeedData.Pois.Select(MapSeedPoi));
            return;
        }

        var existingByQr = existingPois
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .GroupBy(x => NormalizeKey(x.QrCode))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var existingByIdentity = existingPois
            .GroupBy(x => BuildIdentityKey(x.Name, x.Address))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var seedPoi in SeedData.Pois)
        {
            var qrKey = NormalizeKey(seedPoi.QrCode);
            var identityKey = BuildIdentityKey(seedPoi.Name, seedPoi.Address);
            var existing = !string.IsNullOrWhiteSpace(qrKey) && existingByQr.TryGetValue(qrKey, out var byQr)
                ? byQr
                : !string.IsNullOrWhiteSpace(identityKey) && existingByIdentity.TryGetValue(identityKey, out var byIdentity)
                    ? byIdentity
                    : null;

            if (existing is null)
            {
                var newPoi = MapSeedPoi(seedPoi);
                db.Pois.Add(newPoi);

                if (!string.IsNullOrWhiteSpace(qrKey))
                    existingByQr[qrKey] = newPoi;

                if (!string.IsNullOrWhiteSpace(identityKey))
                    existingByIdentity[identityKey] = newPoi;

                continue;
            }

            ApplySeedPoi(existing, seedPoi);
        }
    }

    private static Poi MapSeedPoi(SeedPoiData source)
    {
        var poi = new Poi();
        ApplySeedPoi(poi, source);
        return poi;
    }

    private static void ApplySeedPoi(Poi target, SeedPoiData source)
    {
        target.Name = source.Name;
        target.Address = source.Address;
        target.PhoneNumber = string.IsNullOrWhiteSpace(source.PhoneNumber)
            ? target.PhoneNumber
            : source.PhoneNumber;
        target.Latitude = source.Latitude;
        target.Longitude = source.Longitude;
        target.RadiusMeters = source.RadiusMeters;
        target.Priority = source.Priority;
        target.Description = source.Description;
        target.TtsScriptVi = source.TtsScriptVi;
        target.TtsScriptEn = source.TtsScriptEn;
        target.TtsScriptZh = source.TtsScriptZh;
        target.TtsScriptJa = source.TtsScriptJa;
        target.TtsScriptDe = source.TtsScriptDe;
        target.ImageUrl = source.ImageUrl;
        target.QrCode = source.QrCode;
        target.IsActive = source.IsActive;
        target.MapUrl = CreateMapUrl(source.Latitude, source.Longitude);
    }

    private static string CreateMapUrl(double latitude, double longitude)
        => $"https://maps.google.com/?q={latitude},{longitude}";

    private static string BuildIdentityKey(string? name, string? address)
        => $"{NormalizeKey(name)}|{NormalizeKey(address)}";

    private static string NormalizeKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
