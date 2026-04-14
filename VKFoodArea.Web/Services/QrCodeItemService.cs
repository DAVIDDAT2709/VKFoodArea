using Microsoft.AspNetCore.Http;
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
    private readonly IQrCodeImageStorageService _qrCodeImageStorageService;

    public QrCodeItemService(
        AppDbContext context,
        IQrCodeImageStorageService qrCodeImageStorageService)
    {
        _context = context;
        _qrCodeImageStorageService = qrCodeImageStorageService;
    }

    public async Task<List<QrCodeItemListItemViewModel>> GetAllAsync()
    {
        var items = await _context.QrCodeItems
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        var poiLookup = await LoadPoiLookupAsync(items
            .Where(x => QrTargetTypes.Normalize(x.TargetType) == QrTargetTypes.Poi)
            .Select(x => x.TargetId));

        var tourLookup = await LoadTourLookupAsync(items
            .Where(x => QrTargetTypes.Normalize(x.TargetType) == QrTargetTypes.Tour)
            .Select(x => x.TargetId));

        return items
            .Select(item => new QrCodeItemListItemViewModel
            {
                Id = item.Id,
                Code = item.Code,
                Title = item.Title,
                ImageUrl = item.ImageUrl,
                TargetType = QrTargetTypes.Normalize(item.TargetType),
                TargetId = item.TargetId,
                TargetName = ResolveTargetName(item.TargetType, item.TargetId, poiLookup, tourLookup),
                IsTargetActive = ResolveTargetActive(item.TargetType, item.TargetId, poiLookup, tourLookup),
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt
            })
            .ToList();
    }

    public async Task<QrCodeItemFormViewModel> BuildCreateFormAsync()
    {
        return new QrCodeItemFormViewModel
        {
            IsActive = true,
            TargetType = QrTargetTypes.Poi,
            TargetTypeOptions = BuildTargetTypeOptions(QrTargetTypes.Poi),
            PoiOptions = await GetPoiOptionsAsync(),
            TourOptions = await GetTourOptionsAsync()
        };
    }

    public async Task<QrCodeItemFormViewModel?> GetEditFormAsync(int id)
    {
        var entity = await _context.QrCodeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return null;

        var targetType = QrTargetTypes.Normalize(entity.TargetType);

        return new QrCodeItemFormViewModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            CurrentImageUrl = entity.ImageUrl,
            TargetType = targetType,
            PoiId = targetType == QrTargetTypes.Poi ? entity.TargetId : null,
            TourId = targetType == QrTargetTypes.Tour ? entity.TargetId : null,
            IsActive = entity.IsActive,
            TargetTypeOptions = BuildTargetTypeOptions(targetType),
            PoiOptions = await GetPoiOptionsAsync(),
            TourOptions = await GetTourOptionsAsync()
        };
    }

    public async Task<QrCodeItemDeleteViewModel?> GetDeleteModelAsync(int id)
    {
        var entity = await _context.QrCodeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return null;

        var poiLookup = await LoadPoiLookupAsync([entity.TargetId]);
        var tourLookup = await LoadTourLookupAsync([entity.TargetId]);

        return new QrCodeItemDeleteViewModel
        {
            Id = entity.Id,
            Code = entity.Code,
            Title = entity.Title,
            TargetType = QrTargetTypes.Normalize(entity.TargetType),
            TargetId = entity.TargetId,
            TargetName = ResolveTargetName(entity.TargetType, entity.TargetId, poiLookup, tourLookup),
            IsTargetActive = ResolveTargetActive(entity.TargetType, entity.TargetId, poiLookup, tourLookup),
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt
        };
    }

    public string? ValidateImageFile(IFormFile? imageFile)
        => _qrCodeImageStorageService.Validate(imageFile);

    public async Task<(bool Success, string? Error)> CreateAsync(QrCodeItemFormViewModel vm)
    {
        var target = await ResolveTargetAsync(vm);
        if (!target.Success)
            return (false, target.Error);

        var validationError = await ValidateAsync(null, vm, target.TargetType, target.TargetId, target.IsTargetActive);
        if (!string.IsNullOrWhiteSpace(validationError))
            return (false, validationError);

        var normalizedCode = QrCodeHelper.Normalize(vm.Code);
        var entity = new QrCodeItem
        {
            Code = normalizedCode,
            Title = (vm.Title ?? string.Empty).Trim(),
            ImageUrl = await SaveImageOrKeepExistingAsync(vm.ImageFile, null, normalizedCode),
            TargetType = target.TargetType,
            TargetId = target.TargetId,
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

        var target = await ResolveTargetAsync(vm);
        if (!target.Success)
            return (false, target.Error);

        var validationError = await ValidateAsync(id, vm, target.TargetType, target.TargetId, target.IsTargetActive);
        if (!string.IsNullOrWhiteSpace(validationError))
            return (false, validationError);

        var normalizedCode = QrCodeHelper.Normalize(vm.Code);
        entity.Code = normalizedCode;
        entity.Title = (vm.Title ?? string.Empty).Trim();
        entity.ImageUrl = await SaveImageOrKeepExistingAsync(vm.ImageFile, entity.ImageUrl, normalizedCode);
        entity.TargetType = target.TargetType;
        entity.TargetId = target.TargetId;
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

    private async Task<string?> ValidateAsync(
        int? currentId,
        QrCodeItemFormViewModel vm,
        string targetType,
        int targetId,
        bool isTargetActive)
    {
        var code = QrCodeHelper.Normalize(vm.Code);

        if (string.IsNullOrWhiteSpace(code))
            return "Mã QR không hợp lệ.";

        if (!QrTargetTypes.IsValid(targetType))
            return "Loại nội dung QR không hợp lệ.";

        if (targetId <= 0)
            return "Đích đến không hợp lệ.";

        if (vm.IsActive && !isTargetActive)
            return "QR đang hoạt động phải liên kết với nội dung đang hoạt động.";

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
            return "Mã QR đang trùng với mã QR mặc định của một POI cũ.";

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

    private async Task<(bool Success, string TargetType, int TargetId, bool IsTargetActive, string? Error)> ResolveTargetAsync(QrCodeItemFormViewModel vm)
    {
        var targetType = QrTargetTypes.Normalize(vm.TargetType);

        if (targetType == QrTargetTypes.Tour)
        {
            if (!vm.TourId.HasValue || vm.TourId.Value <= 0)
                return (false, targetType, 0, false, "Vui lòng chọn tour.");

            var tour = await _context.Tours
                .AsNoTracking()
                .Where(x => x.Id == vm.TourId.Value)
                .Select(x => new { x.Id, x.IsActive })
                .FirstOrDefaultAsync();

            return tour is null
                ? (false, targetType, 0, false, "Tour không tồn tại.")
                : (true, targetType, tour.Id, tour.IsActive, null);
        }

        if (!vm.PoiId.HasValue || vm.PoiId.Value <= 0)
            return (false, QrTargetTypes.Poi, 0, false, "Vui lòng chọn điểm POI.");

        var poi = await _context.Pois
            .AsNoTracking()
            .Where(x => x.Id == vm.PoiId.Value)
            .Select(x => new { x.Id, x.IsActive })
            .FirstOrDefaultAsync();

        return poi is null
            ? (false, QrTargetTypes.Poi, 0, false, "Điểm POI không tồn tại.")
            : (true, QrTargetTypes.Poi, poi.Id, poi.IsActive, null);
    }

    private async Task<List<SelectListItem>> GetTourOptionsAsync()
    {
        return await _context.Tours
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

    private static List<SelectListItem> BuildTargetTypeOptions(string selected)
    {
        return
        [
            new SelectListItem("POI", QrTargetTypes.Poi, selected == QrTargetTypes.Poi),
            new SelectListItem("Tour", QrTargetTypes.Tour, selected == QrTargetTypes.Tour)
        ];
    }

    private async Task<Dictionary<int, (string Name, bool IsActive)>> LoadPoiLookupAsync(IEnumerable<int> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return new Dictionary<int, (string Name, bool IsActive)>();

        return await _context.Pois
            .AsNoTracking()
            .Where(x => distinctIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => (x.Name, x.IsActive));
    }

    private async Task<Dictionary<int, (string Name, bool IsActive)>> LoadTourLookupAsync(IEnumerable<int> ids)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
            return new Dictionary<int, (string Name, bool IsActive)>();

        return await _context.Tours
            .AsNoTracking()
            .Where(x => distinctIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => (x.Name, x.IsActive));
    }

    private static string ResolveTargetName(
        string? targetType,
        int targetId,
        IReadOnlyDictionary<int, (string Name, bool IsActive)> poiLookup,
        IReadOnlyDictionary<int, (string Name, bool IsActive)> tourLookup)
    {
        var normalizedTargetType = QrTargetTypes.Normalize(targetType);
        var lookup = normalizedTargetType == QrTargetTypes.Tour ? tourLookup : poiLookup;

        return lookup.TryGetValue(targetId, out var item)
            ? item.Name
            : normalizedTargetType == QrTargetTypes.Tour
                ? "Tour không tồn tại"
                : "POI không tồn tại";
    }

    private static bool ResolveTargetActive(
        string? targetType,
        int targetId,
        IReadOnlyDictionary<int, (string Name, bool IsActive)> poiLookup,
        IReadOnlyDictionary<int, (string Name, bool IsActive)> tourLookup)
    {
        var normalizedTargetType = QrTargetTypes.Normalize(targetType);
        var lookup = normalizedTargetType == QrTargetTypes.Tour ? tourLookup : poiLookup;
        return lookup.TryGetValue(targetId, out var item) && item.IsActive;
    }

    private async Task<string> SaveImageOrKeepExistingAsync(
        IFormFile? imageFile,
        string? currentImageUrl,
        string normalizedCode)
    {
        if (imageFile is null)
            return (currentImageUrl ?? string.Empty).Trim();

        return await _qrCodeImageStorageService.SaveAsync(imageFile, normalizedCode);
    }
}
