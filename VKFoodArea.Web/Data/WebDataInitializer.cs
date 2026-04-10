using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using VKFoodArea.Data;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.Data;

public static class WebDataInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        await EnsureNarrationHistoryUserKeyColumnAsync(db);

        if (!await db.Pois.AnyAsync())
        {
            db.Pois.AddRange(SeedData.Pois.Select(MapPoi));
            await db.SaveChangesAsync();
            return;
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("VKFOODAREA_IMPORT_SEED_POIS"),
                "1",
                StringComparison.Ordinal))
        {
            await ImportMissingSeedPoisAsync(db);
        }
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
        QrCode = source.QrCode,
        IsActive = source.IsActive
    };
}
