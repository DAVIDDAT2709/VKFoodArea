using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class UserMovementLogService : IUserMovementLogService
{
    private readonly AppDbContext _context;

    public UserMovementLogService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MovementLogApiViewModel> CreateFromAppAsync(MovementLogCreateApiViewModel vm)
    {
        var entity = new UserMovementLog
        {
            UserKey = MovementLogUserKeyPrivacy.NormalizeForStorage(vm.UserKey),
            Latitude = vm.Latitude,
            Longitude = vm.Longitude,
            AccuracyMeters = vm.AccuracyMeters,
            Source = NormalizeSource(vm.Source),
            RecordedAt = vm.RecordedAt ?? DateTime.UtcNow
        };

        _context.UserMovementLogs.Add(entity);
        await _context.SaveChangesAsync();

        return MapToApi(entity);
    }

    public async Task<List<MovementLogApiViewModel>> GetRecentAsync(int top = 200)
    {
        var normalizedTop = Math.Clamp(top, 1, 1000);

        return await _context.UserMovementLogs
            .AsNoTracking()
            .OrderByDescending(x => x.RecordedAt)
            .Take(normalizedTop)
            .Select(x => new MovementLogApiViewModel
            {
                Id = x.Id,
                UserKey = MovementLogUserKeyPrivacy.NormalizeForStorage(x.UserKey),
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                AccuracyMeters = x.AccuracyMeters,
                Source = x.Source,
                RecordedAt = x.RecordedAt
            })
            .ToListAsync();
    }

    private static MovementLogApiViewModel MapToApi(UserMovementLog entity) => new()
    {
        Id = entity.Id,
        UserKey = MovementLogUserKeyPrivacy.NormalizeForStorage(entity.UserKey),
        Latitude = entity.Latitude,
        Longitude = entity.Longitude,
        AccuracyMeters = entity.AccuracyMeters,
        Source = entity.Source,
        RecordedAt = entity.RecordedAt
    };

    private static string NormalizeSource(string? source)
    {
        return (source ?? "gps").Trim().ToLowerInvariant() switch
        {
            "background" => "background",
            "foreground" => "foreground",
            "gps" => "gps",
            _ => "gps"
        };
    }
}
