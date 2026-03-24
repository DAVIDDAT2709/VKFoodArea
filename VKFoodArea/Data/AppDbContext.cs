using Microsoft.EntityFrameworkCore;
using VKFoodArea.Models;

namespace VKFoodArea.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<GeofenceEventLog> GeofenceEventLogs => Set<GeofenceEventLog>();
    public DbSet<NarrationLog> NarrationLogs => Set<NarrationLog>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
}