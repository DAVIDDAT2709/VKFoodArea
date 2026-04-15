using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Models;

namespace VKFoodArea.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<QrCodeItem> QrCodeItems => Set<QrCodeItem>();
    public DbSet<NarrationHistory> NarrationHistories => Set<NarrationHistory>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AppUserAccount> AppUserAccounts => Set<AppUserAccount>();
    public DbSet<PoiTranslation> PoiTranslations => Set<PoiTranslation>();
    public DbSet<PoiAudioAsset> PoiAudioAssets => Set<PoiAudioAsset>();
    public DbSet<UserMovementLog> UserMovementLogs => Set<UserMovementLog>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourStop> TourStops => Set<TourStop>();
    public DbSet<AppDeviceSession> AppDeviceSessions => Set<AppDeviceSession>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QrCodeItem>()
            .Property(x => x.TargetType)
            .HasMaxLength(20);

        modelBuilder.Entity<QrCodeItem>()
            .HasIndex(x => x.Code)
            .IsUnique();

        modelBuilder.Entity<QrCodeItem>()
            .HasIndex(x => new { x.TargetType, x.TargetId });

        modelBuilder.Entity<NarrationHistory>()
            .HasOne(x => x.Poi)
            .WithMany(x => x.NarrationHistories)
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AdminUser>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<AppUserAccount>()
            .HasIndex(x => x.UserKey)
            .IsUnique();

        modelBuilder.Entity<PoiTranslation>()
            .HasOne(x => x.Poi)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PoiTranslation>()
            .HasIndex(x => new { x.PoiId, x.Language })
            .IsUnique();

        modelBuilder.Entity<PoiAudioAsset>()
            .HasOne(x => x.Poi)
            .WithMany(x => x.AudioAssets)
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PoiAudioAsset>()
            .HasIndex(x => new { x.PoiId, x.Language })
            .IsUnique();

        modelBuilder.Entity<UserMovementLog>()
            .HasIndex(x => x.RecordedAt);

        modelBuilder.Entity<UserMovementLog>()
            .HasIndex(x => x.UserKey);

        modelBuilder.Entity<Tour>()
            .HasIndex(x => x.Name);

        modelBuilder.Entity<TourStop>()
            .HasOne(x => x.Tour)
            .WithMany(x => x.Stops)
            .HasForeignKey(x => x.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TourStop>()
            .HasOne(x => x.Poi)
            .WithMany()
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TourStop>()
            .HasIndex(x => new { x.TourId, x.DisplayOrder })
            .IsUnique();

        modelBuilder.Entity<AppDeviceSession>()
            .HasIndex(x => x.DeviceKey)
            .IsUnique();

        modelBuilder.Entity<AppDeviceSession>()
            .HasIndex(x => x.LastHeartbeatAt);

        modelBuilder.Entity<AppDeviceSession>()
            .HasIndex(x => x.UserKey);
    }
}
