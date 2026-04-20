using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using VKFoodArea.Data;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.Services;

namespace VKFoodArea.Web.Data;

public static class WebDataInitializer
{
    private const string DemoTourName = "Tour Vĩnh Khánh 30 phút";
    private const string DemoTourQrCode = "tour:vinh-khanh-30-phut";

    public static async Task InitializeAsync(AppDbContext db, IWebHostEnvironment environment, bool seedDevelopmentAdmin)
    {
        await db.Database.MigrateAsync();
        await EnsureAdminUsersTableAsync(db);
        await EnsureNarrationHistoryUserKeyColumnAsync(db);
        await EnsureNarrationHistoryTourColumnsAsync(db);
        await EnsureAnonymousMovementLogKeysAsync(db);
        await EnsurePoiAudioColumnsAsync(db);
        await EnsurePoiOwnerColumnAsync(db);
        await EnsurePoiApprovalColumnsAsync(db);
        await EnsureTourNarrationColumnsAsync(db);
        await EnsureQrCodeImageColumnAsync(db);
        await SyncPoiContentTablesAsync(db);
        await SeedDefaultAdminAsync(db, seedDevelopmentAdmin);

        if (!await db.Pois.AnyAsync())
        {
            db.Pois.AddRange(SeedData.Pois.Select(MapPoi));
            await db.SaveChangesAsync();
        }
        else if (string.Equals(
                Environment.GetEnvironmentVariable("VKFOODAREA_IMPORT_SEED_POIS"),
                "1",
                StringComparison.Ordinal))
        {
            await ImportMissingSeedPoisAsync(db);
        }

        await RefreshSeedPoiContentAsync(db);
        await EnsurePoiUniquenessIndexesAsync(db);
        await EnsurePoiImageUrlsAsync(db, environment);
        await SyncPoiContentTablesAsync(db);
        await SeedDemoPathAsync(db);
    }

    private static async Task ImportMissingSeedPoisAsync(AppDbContext db)
    {
        var existingQrCodes = await db.Pois
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .Select(x => x.QrCode)
            .ToListAsync();

        var existingSet = existingQrCodes
            .Select(NormalizeQrCode)
            .ToHashSet(StringComparer.Ordinal);
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

    private static async Task RefreshSeedPoiContentAsync(AppDbContext db)
    {
        var seedPois = SeedData.Pois.ToList();
        var existingPois = await db.Pois.ToListAsync();

        var existingByQr = existingPois
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .GroupBy(x => NormalizeQrCode(x.QrCode))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var existingByIdentity = existingPois
            .GroupBy(x => BuildIdentityKey(x.Name, x.Address))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        foreach (var seedPoi in seedPois)
        {
            var qrKey = NormalizeQrCode(seedPoi.QrCode);
            var identityKey = BuildIdentityKey(seedPoi.Name, seedPoi.Address);
            var existing = !string.IsNullOrWhiteSpace(qrKey) && existingByQr.TryGetValue(qrKey, out var byQr)
                ? byQr
                : !string.IsNullOrWhiteSpace(identityKey) && existingByIdentity.TryGetValue(identityKey, out var byIdentity)
                    ? byIdentity
                    : null;

            if (existing is null)
                continue;

            BackfillSeedPoiContent(existing, seedPoi);
        }

        await db.SaveChangesAsync();
    }

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

    private static async Task EnsureNarrationHistoryTourColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "NarrationHistories", "TourId"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE NarrationHistories ADD COLUMN TourId INTEGER NULL;");

        if (!await HasColumnAsync(connection, "NarrationHistories", "TourName"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE NarrationHistories ADD COLUMN TourName TEXT NOT NULL DEFAULT '';");
    }

    private static async Task EnsureAnonymousMovementLogKeysAsync(AppDbContext db)
    {
        var movementLogs = await db.UserMovementLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.UserKey))
            .ToListAsync();

        var hasChanges = false;

        foreach (var movementLog in movementLogs)
        {
            var anonymizedKey = MovementLogUserKeyPrivacy.NormalizeForStorage(movementLog.UserKey);
            if (string.Equals(movementLog.UserKey, anonymizedKey, StringComparison.Ordinal))
                continue;

            movementLog.UserKey = anonymizedKey;
            hasChanges = true;
        }

        if (hasChanges)
            await db.SaveChangesAsync();
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

    private static async Task EnsurePoiOwnerColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "Pois", "OwnerAdminUserId"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN OwnerAdminUserId INTEGER NULL;");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Pois_OwnerAdminUserId ON Pois (OwnerAdminUserId);");
    }

    private static async Task EnsurePoiApprovalColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "Pois", "ApprovalStatus"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN ApprovalStatus TEXT NOT NULL DEFAULT 'Approved';");

        if (!await HasColumnAsync(connection, "Pois", "SubmittedAt"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN SubmittedAt TEXT NULL;");

        if (!await HasColumnAsync(connection, "Pois", "ReviewedAt"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN ReviewedAt TEXT NULL;");

        if (!await HasColumnAsync(connection, "Pois", "ReviewedByAdminUserId"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN ReviewedByAdminUserId INTEGER NULL;");

        if (!await HasColumnAsync(connection, "Pois", "ReviewNote"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Pois ADD COLUMN ReviewNote TEXT NOT NULL DEFAULT '';");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Pois_ApprovalStatus ON Pois (ApprovalStatus);");

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE Pois
            SET ApprovalStatus = 'Approved'
            WHERE ApprovalStatus IS NULL OR trim(ApprovalStatus) = '';
            """);
    }

    private static async Task EnsurePoiUniquenessIndexesAsync(AppDbContext db)
    {
        var pois = await db.Pois
            .AsNoTracking()
            .Select(x => new
            {
                x.Name,
                x.Address,
                x.Latitude,
                x.Longitude,
                x.QrCode
            })
            .ToListAsync();

        var hasIdentityDuplicates = pois
            .GroupBy(x => PoiIdentityHelper.BuildIdentityKey(x.Name, x.Address))
            .Any(x =>
                !string.IsNullOrWhiteSpace(x.Key.Replace("|", string.Empty, StringComparison.Ordinal)) &&
                x.Count() > 1);

        var hasNameDuplicates = pois
            .GroupBy(x => PoiIdentityHelper.NormalizeIdentityText(x.Name))
            .Any(x =>
                !string.IsNullOrWhiteSpace(x.Key) &&
                x.Count() > 1);

        if (!hasNameDuplicates)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS UX_Pois_Name
                ON Pois (lower(trim(Name)))
                WHERE trim(Name) <> '';
                """);
        }

        if (!hasIdentityDuplicates)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS UX_Pois_NameAddress
                ON Pois (lower(trim(Name)), lower(trim(Address)))
                WHERE trim(Name) <> '' AND trim(Address) <> '';
                """);
        }

        var hasCoordinateDuplicates = pois
            .GroupBy(x => PoiIdentityHelper.BuildCoordinateKey(x.Latitude, x.Longitude))
            .Any(x => x.Count() > 1);

        if (!hasCoordinateDuplicates)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS UX_Pois_Coordinates
                ON Pois (printf('%.6f', Latitude), printf('%.6f', Longitude));
                """);
        }

        var hasQrDuplicates = pois
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .GroupBy(x => NormalizeQrCode(x.QrCode))
            .Any(x => x.Count() > 1);

        if (!hasQrDuplicates)
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS UX_Pois_QrCode
                ON Pois (lower(trim(QrCode)))
                WHERE trim(QrCode) <> '';
                """);
        }
    }

    private static async Task EnsureQrCodeImageColumnAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "QrCodeItems", "ImageUrl"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE QrCodeItems ADD COLUMN ImageUrl TEXT NOT NULL DEFAULT '';");
    }

    private static async Task EnsureTourNarrationColumnsAsync(AppDbContext db)
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        if (!await HasColumnAsync(connection, "Tours", "TtsScriptVi"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tours ADD COLUMN TtsScriptVi TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Tours", "TtsScriptEn"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tours ADD COLUMN TtsScriptEn TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Tours", "TtsScriptZh"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tours ADD COLUMN TtsScriptZh TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Tours", "TtsScriptJa"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tours ADD COLUMN TtsScriptJa TEXT NOT NULL DEFAULT '';");

        if (!await HasColumnAsync(connection, "Tours", "TtsScriptDe"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Tours ADD COLUMN TtsScriptDe TEXT NOT NULL DEFAULT '';");
    }

    private static async Task EnsurePoiImageUrlsAsync(AppDbContext db, IWebHostEnvironment environment)
    {
        var webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var webImagesDirectory = Path.Combine(webRootPath, "uploads", "poi-images");
        var appImagesDirectory = Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            "..",
            "VKFoodArea",
            "Resources",
            "Images"));

        Directory.CreateDirectory(webImagesDirectory);

        var pois = await db.Pois.ToListAsync();
        var hasChanges = false;

        foreach (var poi in pois)
        {
            var resolvedImageUrl = await ResolvePoiImageUrlAsync(
                poi.ImageUrl,
                webImagesDirectory,
                appImagesDirectory);

            if (string.Equals(poi.ImageUrl, resolvedImageUrl, StringComparison.Ordinal))
                continue;

            poi.ImageUrl = resolvedImageUrl;
            hasChanges = true;
        }

        if (hasChanges)
            await db.SaveChangesAsync();
    }

    private static async Task<string> ResolvePoiImageUrlAsync(
        string? imageUrl,
        string webImagesDirectory,
        string appImagesDirectory)
    {
        var normalized = (imageUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (Uri.TryCreate(normalized, UriKind.Absolute, out _))
            return normalized;

        if (normalized.StartsWith('/'))
            return normalized;

        var webRelativePath = normalized.Replace('\\', '/').TrimStart('/');
        if (webRelativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return $"/{webRelativePath}";

        var fileName = await EnsureImageFileAvailableAsync(normalized, webImagesDirectory, appImagesDirectory);
        return string.IsNullOrWhiteSpace(fileName)
            ? normalized
            : $"/uploads/poi-images/{fileName}";
    }

    private static async Task<string> EnsureImageFileAvailableAsync(
        string fileReference,
        string webImagesDirectory,
        string appImagesDirectory)
    {
        var normalizedName = Path.GetFileName(fileReference);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return string.Empty;

        var webFilePath = FindImageByFileName(webImagesDirectory, normalizedName);
        if (!string.IsNullOrWhiteSpace(webFilePath))
            return Path.GetFileName(webFilePath);

        var appFilePath = FindImageByFileName(appImagesDirectory, normalizedName);
        if (string.IsNullOrWhiteSpace(appFilePath))
            return string.Empty;

        var destinationPath = Path.Combine(webImagesDirectory, Path.GetFileName(appFilePath));
        if (!File.Exists(destinationPath))
        {
            await using var sourceStream = File.OpenRead(appFilePath);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream);
        }

        return Path.GetFileName(destinationPath);
    }

    private static string? FindImageByFileName(string directory, string fileName)
    {
        if (!Directory.Exists(directory))
            return null;

        var exactPath = Path.Combine(directory, fileName);
        if (File.Exists(exactPath))
            return exactPath;

        var requestedStem = Path.GetFileNameWithoutExtension(fileName);
        return Directory.GetFiles(directory)
            .FirstOrDefault(path =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    requestedStem,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SyncPoiContentTablesAsync(AppDbContext db)
    {
        var pois = await db.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets)
            .AsSplitQuery()
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

    private static async Task SeedDemoPathAsync(AppDbContext db)
    {
        var seedQrCodes = SeedData.Pois
            .Select(x => NormalizeQrCode(x.QrCode))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        var seedPois = await db.Pois
            .Where(x => !string.IsNullOrWhiteSpace(x.QrCode))
            .ToListAsync();

        seedPois = seedPois
            .Where(x => seedQrCodes.Contains(NormalizeQrCode(x.QrCode)))
            .ToList();

        var qrItems = await db.QrCodeItems.ToListAsync();
        foreach (var poi in seedPois)
        {
            UpsertQrCodeItem(
                db,
                qrItems,
                poi.QrCode,
                poi.Name,
                QrTargetTypes.Poi,
                poi.Id,
                poi.ImageUrl);
        }

        var stopDefinitions = new[]
        {
            new DemoTourStopDefinition("poi:oc-vu", 1, "Bắt đầu bằng món dễ gọi, vị me chua ngọt rõ."),
            new DemoTourStopDefinition("poi:oc-thao", 2, "Điểm dừng giữa tour, hợp gọi nghêu hấp sả hoặc món nhẹ."),
            new DemoTourStopDefinition("poi:oc-oanh", 3, "Điểm nổi bật để kết tour, hợp đi nhóm và gọi nhiều món."),
            new DemoTourStopDefinition("poi:ot-xiem-quan", 4, "Điểm đổi vị nếu muốn món nóng và no hơn.")
        };

        var seedPoiByQr = seedPois
            .GroupBy(x => NormalizeQrCode(x.QrCode))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var tourStops = stopDefinitions
            .Select(x => new
            {
                Definition = x,
                Poi = seedPoiByQr.GetValueOrDefault(NormalizeQrCode(x.QrCode))
            })
            .Where(x => x.Poi is not null)
            .ToList();

        if (tourStops.Count >= 2)
        {
            var tour = await db.Tours
                .Include(x => x.Stops)
                .FirstOrDefaultAsync(x => x.Name == DemoTourName);

            if (tour is null)
            {
                tour = new Tour
                {
                    CreatedAt = DateTime.UtcNow
                };
                db.Tours.Add(tour);
            }

            tour.Name = DemoTourName;
            tour.Description = "Lộ trình mẫu cho buổi demo: bắt đầu bằng quán ốc dễ gọi, đi qua một điểm địa phương, kết ở điểm nổi bật và có lựa chọn đổi vị.";
            tour.TtsScriptVi = "Tour Vĩnh Khánh bắt đầu. Bạn sẽ đi qua vài điểm ăn dễ gọi, có món gợi ý và khoảng cách rõ ràng trên bản đồ. Hãy bật GPS nếu muốn app tự nhận điểm gần nhất khi đến nơi.";
            tour.TtsScriptEn = "The Vinh Khanh demo tour starts now. You will pass several easy-to-order food stops with practical suggestions and clear map guidance.";
            tour.TtsScriptZh = string.Empty;
            tour.TtsScriptJa = string.Empty;
            tour.TtsScriptDe = string.Empty;
            tour.IsActive = true;

            SyncDemoTourStops(tour, tourStops.Select(x => (x.Definition, Poi: x.Poi!)).ToList());
            await db.SaveChangesAsync();

            qrItems = await db.QrCodeItems.ToListAsync();
            UpsertQrCodeItem(
                db,
                qrItems,
                DemoTourQrCode,
                DemoTourName,
                QrTargetTypes.Tour,
                tour.Id,
                string.Empty);
        }

        await db.SaveChangesAsync();
    }

    private static void SyncDemoTourStops(Tour tour, IReadOnlyList<(DemoTourStopDefinition Definition, Poi Poi)> stops)
    {
        var desiredOrders = stops
            .Select(x => x.Definition.DisplayOrder)
            .ToHashSet();

        foreach (var stop in tour.Stops.Where(x => !desiredOrders.Contains(x.DisplayOrder)).ToList())
            tour.Stops.Remove(stop);

        foreach (var stop in stops.OrderBy(x => x.Definition.DisplayOrder))
        {
            var existing = tour.Stops.FirstOrDefault(x => x.DisplayOrder == stop.Definition.DisplayOrder);
            if (existing is null)
            {
                tour.Stops.Add(new TourStop
                {
                    DisplayOrder = stop.Definition.DisplayOrder,
                    PoiId = stop.Poi.Id,
                    Note = stop.Definition.Note
                });
                continue;
            }

            existing.PoiId = stop.Poi.Id;
            existing.Note = stop.Definition.Note;
        }
    }

    private static void UpsertQrCodeItem(
        AppDbContext db,
        List<QrCodeItem> qrItems,
        string? code,
        string title,
        string targetType,
        int targetId,
        string? imageUrl)
    {
        var normalizedCode = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode) || targetId <= 0)
            return;

        var existing = qrItems.FirstOrDefault(x =>
            NormalizeQrCode(x.Code) == NormalizeQrCode(normalizedCode));

        if (existing is null)
        {
            existing = new QrCodeItem
            {
                Code = normalizedCode,
                CreatedAt = DateTime.Now
            };
            qrItems.Add(existing);
            db.QrCodeItems.Add(existing);
        }

        existing.Title = title.Trim();
        existing.TargetType = QrTargetTypes.Normalize(targetType);
        existing.TargetId = targetId;
        existing.ImageUrl = imageUrl ?? string.Empty;
        existing.IsActive = true;
    }

    private sealed record DemoTourStopDefinition(string QrCode, int DisplayOrder, string Note);

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

    private static Poi MapPoi(SeedPoiData source)
    {
        var poi = new Poi
        {
            AudioFileVi = string.Empty,
            AudioFileEn = string.Empty,
            AudioFileJa = string.Empty,
            ApprovalStatus = PoiApprovalStatus.Approved,
            SubmittedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow
        };

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
        target.ImageUrl = source.ImageUrl;
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
        target.QrCode = source.QrCode;
        target.IsActive = source.IsActive;
    }

    private static void BackfillSeedPoiContent(Poi target, SeedPoiData source)
    {
        if (string.IsNullOrWhiteSpace(target.PhoneNumber))
            target.PhoneNumber = source.PhoneNumber;

        if (string.IsNullOrWhiteSpace(target.ImageUrl))
            target.ImageUrl = source.ImageUrl;

        if (Math.Abs(target.Latitude) < double.Epsilon && Math.Abs(target.Longitude) < double.Epsilon)
        {
            target.Latitude = source.Latitude;
            target.Longitude = source.Longitude;
        }

        if (target.RadiusMeters <= 0)
            target.RadiusMeters = source.RadiusMeters;

        if (target.Priority <= 0)
            target.Priority = source.Priority;

        if (string.IsNullOrWhiteSpace(target.Description))
            target.Description = source.Description;

        if (string.IsNullOrWhiteSpace(target.TtsScriptVi))
            target.TtsScriptVi = source.TtsScriptVi;

        if (string.IsNullOrWhiteSpace(target.TtsScriptEn))
            target.TtsScriptEn = source.TtsScriptEn;

        if (string.IsNullOrWhiteSpace(target.TtsScriptZh))
            target.TtsScriptZh = source.TtsScriptZh;

        if (string.IsNullOrWhiteSpace(target.TtsScriptJa))
            target.TtsScriptJa = source.TtsScriptJa;

        if (string.IsNullOrWhiteSpace(target.TtsScriptDe))
            target.TtsScriptDe = source.TtsScriptDe;

        if (string.IsNullOrWhiteSpace(target.QrCode))
            target.QrCode = source.QrCode;
    }

    private static string BuildIdentityKey(string? name, string? address)
        => PoiIdentityHelper.BuildIdentityKey(name, address);
}
