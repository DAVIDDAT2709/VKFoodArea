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
            .OrderByDescending(x => x.CreatedAt)
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

    public async Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm)
    {
        var code = QrCodeHelper.Normalize(vm.Code);

        if (string.IsNullOrWhiteSpace(code))
            return (false, "Mã QR không hợp lệ.");

        var poi = await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == vm.PoiId);

        if (poi is null)
            return (false, "POI không tồn tại.");

        var existsInQrItems = await _context.QrCodeItems
            .AnyAsync(x => !string.IsNullOrWhiteSpace(x.Code) && x.Code.ToLower() == code);

        if (existsInQrItems)
            return (false, "Mã QR đã tồn tại trong danh sách QR Code.");

        var existsInPois = await _context.Pois
            .AnyAsync(x => !string.IsNullOrWhiteSpace(x.QrCode) && x.QrCode.ToLower() == code);

        if (existsInPois)
            return (false, "Mã QR đã trùng với mã QR mặc định của một POI.");

        var entity = new QrCodeItem
        {
            Code = code,
            Title = vm.Title.Trim(),
            PoiId = vm.PoiId,
            IsActive = vm.IsActive
        };

        _context.QrCodeItems.Add(entity);
        await _context.SaveChangesAsync();

        return (true, null);
    }

    private async Task<List<SelectListItem>> GetPoiOptionsAsync()
    {
        return await _context.Pois
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();
    }
}