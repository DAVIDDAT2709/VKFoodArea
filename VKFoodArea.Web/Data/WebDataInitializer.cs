using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using VKFoodArea.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Data;

public static class WebDataInitializer
{
    public static async Task InitializeAsync(AppDbContext db, bool seedDevelopmentAdmin)
    {
        await db.Database.MigrateAsync();
        await EnsureAdminUsersTableAsync(db);
        await EnsureNarrationHistoryUserKeyColumnAsync(db);
        await EnsurePoiAudioColumnsAsync(db);
        await SyncPoiContentTablesAsync(db);
        await SeedDefaultAdminAsync(db, seedDevelopmentAdmin);

        if (!await db.Pois.AnyAsync())
        {
            db.Pois.AddRange(SeedData.Pois.Select(MapPoi));
            await db.SaveChangesAsync();
            await SyncPoiContentTablesAsync(db);
            return;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("VKFOODAREA_IMPORT_SEED_POIS"),
                "1",
                StringComparison.Ordinal))
        {
            await ImportMissingSeedPoisAsync(db);
        }

        await SyncPoiContentTablesAsync(db);
    }

    private static async Task ImportMissingSeedPoisAsync(AppDbContext db)
    {
        var existingQrCodes = await db.Pois
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .Select(x => x.QrCode.Trim().ToLower())
            .ToListAsync();

        var existingSet = existingQrCodes.ToHashSet(StringComparer.Ordinal);
        var missingPois = SeedData.Pois
            .Where(x => !existingSet.Contains(NormalizeQrCode(x.QrCode)))
            .Select(MapPoi)
            .ToList();

        if (missingPois.Count == 0)
            return;

        db.Pois.AddRange(missingPois);
        await db.SaveChangesAsync();
    }

    private static string NormalizeQrCode(string? qrCode)
        => (qrCode ?? string.Empty).Trim().ToLowerInvariant();

    private static async Task EnsureNarrationHistoryUserKeyColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "NarrationHistories", "UserKey"))
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE NarrationHistories ADD COLUMN UserKey TEXT NOT NULL DEFAULT '';");
        }
    }

    private static async Task EnsureAdminUsersTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS AdminUsers (
                Id INTEGER NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                FullName TEXT NOT NULL DEFAULT '',
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'Admin',
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                LastLoginAt TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_AdminUsers_Username ON AdminUsers (Username);");
    }

    private static async Task EnsurePoiAudioColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "Pois", "AudioFileVi"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN AudioFileVi TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Pois", "AudioFileEn"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN AudioFileEn TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Pois", "AudioFileJa"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN AudioFileJa TEXT NOT NULL DEFAULT '';");
    }

    private static async Task SyncPoiContentTablesAsync(AppDbContext db)
    {
        var pois = await db.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets)
            .ToListAsync();

        foreach (var poi in pois)
        {
            UpsertTranslation(poi, "vi", poi.TtsScriptVi);
            UpsertTranslation(poi, "en", poi.TtsScriptEn);
            UpsertTranslation(poi, "zh", poi.TtsScriptZh);
            UpsertTranslation(poi, "ja", poi.TtsScriptJa);
            UpsertTranslation(poi, "de", poi.TtsScriptDe);

            UpsertAudioAsset(poi, "vi", poi.AudioFileVi);
            UpsertAudioAsset(poi, "en", poi.AudioFileEn);
            UpsertAudioAsset(poi, "ja", poi.AudioFileJa);
        }

        await db.SaveChangesAsync();
    }

    private static void UpsertTranslation(Poi poi, string language, string? script)
    {
        var normalizedScript = (script ?? string.Empty).Trim();
        var existing = poi.Translations.FirstOrDefault(x => x.Language == language);

        if (string.IsNullOrWhiteSpace(normalizedScript))
        {
            if (existing is not null)
                poi.Translations.Remove(existing);

            return;
        }

        if (existing is null)
        {
            poi.Translations.Add(new PoiTranslation
            {
                Language = language,
                Script = normalizedScript
            });
            return;
        }

        existing.Script = normalizedScript;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static void UpsertAudioAsset(Poi poi, string language, string? fileUrl)
    {
        var normalizedFileUrl = (fileUrl ?? string.Empty).Trim();
        var existing = poi.AudioAssets.FirstOrDefault(x => x.Language == language);

        if (string.IsNullOrWhiteSpace(normalizedFileUrl))
        {
            if (existing is not null)
                poi.AudioAssets.Remove(existing);

            return;
        }

        if (existing is null)
        {
            poi.AudioAssets.Add(new PoiAudioAsset
            {
                Language = language,
                FileUrl = normalizedFileUrl,
                IsActive = true
            });
            return;
        }

        existing.FileUrl = normalizedFileUrl;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task SeedDefaultAdminAsync(AppDbContext db, bool seedDevelopmentAdmin)
    {
        if (await db.AdminUsers.AnyAsync())
            return;

        var allowDefaultSeed = seedDevelopmentAdmin ||
            string.Equals(
                Environment.GetEnvironmentVariable("VKFOODAREA_SEED_DEFAULT_ADMIN"),
                "1",
                StringComparison.Ordinal);

        if (!allowDefaultSeed)
            return;

        var username = Environment.GetEnvironmentVariable("VKFOODAREA_ADMIN_USERNAME") ?? "admin";
        var password = Environment.GetEnvironmentVariable("VKFOODAREA_ADMIN_PASSWORD") ?? "admin123";

        db.AdminUsers.Add(new AdminUser
        {
            Username = username,
            FullName = "CMS Administrator",
            PasswordHash = AdminPasswordHasher.Hash(password),
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
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

    private static Poi MapPoi(SeedPoiData source) => new()
    {
        Name = source.Name,
        Address = source.Address,
        PhoneNumber = source.PhoneNumber,
        ImageUrl = source.ImageUrl,
        Latitude = source.Latitude,
        Longitude = source.Longitude,
        RadiusMeters = source.RadiusMeters,
        Priority = source.Priority,
        Description = source.Description,
        TtsScriptVi = source.TtsScriptVi,
        TtsScriptEn = source.TtsScriptEn,
        TtsScriptZh = source.TtsScriptZh,
        TtsScriptJa = source.TtsScriptJa,
        TtsScriptDe = source.TtsScriptDe,
        AudioFileVi = string.Empty,
        AudioFileEn = string.Empty,
        AudioFileJa = string.Empty,
        QrCode = source.QrCode,
        IsActive = source.IsActive
    };
}
