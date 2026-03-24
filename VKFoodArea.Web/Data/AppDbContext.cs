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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QrCodeItem>()
            .HasOne(x => x.Poi)
            .WithMany(x => x.QrCodeItems)
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NarrationHistory>()
            .HasOne(x => x.Poi)
            .WithMany(x => x.NarrationHistories)
            .HasForeignKey(x => x.PoiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}