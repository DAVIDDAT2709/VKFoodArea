using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;

namespace VKFoodArea.Repositories;

public class FoodRepository
{
    private readonly AppDbContext _db;

    public FoodRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<FoodItem>> GetByCategoryAsync(string category, CancellationToken ct = default)
        => _db.FoodItems
            .Where(x => x.Category == category)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(ct);

    public Task<List<FoodItem>> GetByRestaurantAsync(string restaurantName, CancellationToken ct = default)
        => _db.FoodItems
            .Where(x => x.RestaurantName == restaurantName)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
}