using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Helpers;
using VKFoodArea.Models;

namespace VKFoodArea.Repositories;

public class PoiRepository
{
    private readonly AppDbContext _db;

    public PoiRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<List<Poi>> GetActiveAsync(CancellationToken ct = default)
        => _db.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(ct);

    public Task<Poi?> GetByIdAsync(int id, CancellationToken ct = default)
        => _db.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Poi?> GetByQrCodeAsync(string qrCode, CancellationToken ct = default)
    {
        var normalized = QrCodePayload.Normalize(qrCode);

        return _db.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IsActive &&
                     !string.IsNullOrWhiteSpace(x.QrCode) &&
                     x.QrCode.ToLower() == normalized,
                ct);
    }
}
