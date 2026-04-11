using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class QrCodeItemService : IQrCodeItemService
{
    private readonly AppDbContext _context;

    public QrCodeItemService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<QrCodeItem>> GetAllAsync()
    {
        return await _context.QrCodeItems
            .AsNoTracking()
            .Include(x => x.Poi)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<QrCodeItemFormViewModel> BuildCreateFormAsync()
    {
        return new QrCodeItemFormViewModel
        {
            IsActive = true,
            PoiOptions = await GetPoiOptionsAsync()
        };
    }

    public async Task<QrCodeItemFormViewModel?> GetEditFormAsync(int id)
    {
        var entity = await _context.QrCodeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return null;

        return new QrCodeItemFormViewModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            PoiId = entity.PoiId,
            IsActive = entity.IsActive,
            PoiOptions = await GetPoiOptionsAsync()
        };
    }

    public async Task<QrCodeItem?> GetDeleteModelAsync(int id)
    {
        return await _context.QrCodeItems
            .AsNoTracking()
            .Include(x => x.Poi)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm)
    {
        var validationError = await ValidateAsync(null, vm);
        if (!string.IsNullOrWhiteSpace(validationError))
            return (false, validationError);

        var entity = new QrCodeItem
        {
            Code = QrCodeHelper.Normalize(vm.Code),
            Title = vm.Title.Trim(),
            PoiId = vm.PoiId,
            IsActive = vm.IsActive
        };

        _context.QrCodeItems.Add(entity);
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, QrCodeItemFormViewModel vm)
    {
        var entity = await _context.QrCodeItems.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return (false, "Không tìm thấy QR code.");

        var validationError = await ValidateAsync(id, vm);
        if (!string.IsNullOrWhiteSpace(validationError))
            return (false, validationError);

        entity.Code = QrCodeHelper.Normalize(vm.Code);
        entity.Title = vm.Title.Trim();
        entity.PoiId = vm.PoiId;
        entity.IsActive = vm.IsActive;

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.QrCodeItems.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return false;

        _context.QrCodeItems.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<string?> ValidateAsync(int? currentId, QrCodeItemFormViewModel vm)
    {
        var code = QrCodeHelper.Normalize(vm.Code);

        if (string.IsNullOrWhiteSpace(code))
            return "Mã QR không hợp lệ.";

        var poi = await _context.Pois
            .AsNoTracking()
            .Where(x => x.Id == vm.PoiId)
            .Select(x => new { x.Id, x.IsActive })
            .FirstOrDefaultAsync();

        if (poi is null)
            return "POI không tồn tại.";

        if (vm.IsActive && !poi.IsActive)
            return "QR dang hoat dong phai lien ket voi POI dang hoat dong.";

        var existsInQrItems = await _context.QrCodeItems
            .AnyAsync(x =>
                x.Id != currentId &&
                !string.IsNullOrWhiteSpace(x.Code) &&
                x.Code.ToLower() == code);

        if (existsInQrItems)
            return "Mã QR đã tồn tại trong danh sách QR code.";

        var existsInPois = await _context.Pois
            .AnyAsync(x =>
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == code);

        if (existsInPois)
            return "Mã QR trùng với mã QR mặc định của một POI.";

        return null;
    }

    private async Task<List<SelectListItem>> GetPoiOptionsAsync()
    {
        return await _context.Pois
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.IsActive ? x.Name : $"{x.Name} (ẩn)"
            })
            .ToListAsync();
    }
}
