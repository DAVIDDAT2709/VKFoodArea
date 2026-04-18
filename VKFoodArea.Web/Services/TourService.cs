using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class TourService : ITourService
{
    private readonly AppDbContext _context;
    private readonly ITtsTranslationService _ttsTranslationService;

    public TourService(AppDbContext context, ITtsTranslationService ttsTranslationService)
    {
        _context = context;
        _ttsTranslationService = ttsTranslationService;
    }

    public async Task<List<Tour>> GetAllAsync()
    {
        return await BuildTourQuery()
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<TourFormViewModel> BuildCreateFormAsync()
    {
        return new TourFormViewModel
        {
            IsActive = true,
            PoiOptions = await GetPoiOptionsAsync(),
            Stops =
            [
                new TourStopInputViewModel
                {
                    DisplayOrder = 1
                }
            ]
        };
    }

    public async Task<TourFormViewModel?> GetEditFormAsync(int id)
    {
        var entity = await BuildTourQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return null;

        return new TourFormViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            IsActive = entity.IsActive,
            TtsScriptVi = entity.TtsScriptVi,
            TtsScriptEn = entity.TtsScriptEn,
            TtsScriptZh = entity.TtsScriptZh,
            TtsScriptJa = entity.TtsScriptJa,
            TtsScriptDe = entity.TtsScriptDe,
            PoiOptions = await GetPoiOptionsAsync(),
            Stops = entity.Stops
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new TourStopInputViewModel
                {
                    Id = x.Id,
                    PoiId = x.PoiId,
                    DisplayOrder = x.DisplayOrder,
                    Note = x.Note
                })
                .ToList()
        };
    }

    public Task<Tour?> GetDeleteModelAsync(int id)
        => BuildTourQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

    public async Task<(bool Success, string? Error)> CreateAsync(TourFormViewModel vm)
    {
        var preparedStops = await PrepareStopsAsync(vm);
        if (preparedStops.Error is not null)
            return (false, preparedStops.Error);

        await PopulateGeneratedFieldsAsync(vm);

        var entity = new Tour();
        MapToEntity(entity, vm, preparedStops.Stops);

        _context.Tours.Add(entity);
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(int id, TourFormViewModel vm)
    {
        var entity = await _context.Tours
            .Include(x => x.Stops)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity is null)
            return (false, "Không tìm thấy tour.");

        var preparedStops = await PrepareStopsAsync(vm, id);
        if (preparedStops.Error is not null)
            return (false, preparedStops.Error);

        await PopulateGeneratedFieldsAsync(vm);

        MapToEntity(entity, vm, preparedStops.Stops);
        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _context.Tours.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return false;

        _context.Tours.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<TourDto>> GetActiveForApiAsync()
    {
        var tours = await BuildTourQuery()
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Stops.All(stop =>
                    stop.Poi != null &&
                    stop.Poi.IsActive &&
                    stop.Poi.ApprovalStatus == PoiApprovalStatus.Approved))
            .OrderBy(x => x.Name)
            .ToListAsync();

        return tours.Select(ApiDtoMapper.ToTourDto).ToList();
    }

    public async Task<TourDto?> GetByIdForApiAsync(int id)
    {
        var tour = await BuildTourQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.IsActive &&
                x.Stops.All(stop =>
                    stop.Poi != null &&
                    stop.Poi.IsActive &&
                    stop.Poi.ApprovalStatus == PoiApprovalStatus.Approved));

        return tour is null ? null : ApiDtoMapper.ToTourDto(tour);
    }

    private async Task<(List<TourStop> Stops, string? Error)> PrepareStopsAsync(TourFormViewModel vm, int? currentId = null)
    {
        var normalizedName = (vm.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            return ([], "Tên tour không hợp lệ.");

        var duplicateName = await _context.Tours.AnyAsync(x =>
            x.Id != currentId &&
            x.Name.ToLower() == normalizedName.ToLower());

        if (duplicateName)
            return ([], "Tên tour đã tồn tại.");

        var stopInputs = (vm.Stops ?? [])
            .Where(x => x.PoiId.HasValue || !string.IsNullOrWhiteSpace(x.Note))
            .Select((stop, index) => new TourStopInputViewModel
            {
                Id = stop.Id,
                PoiId = stop.PoiId,
                DisplayOrder = stop.DisplayOrder > 0 ? stop.DisplayOrder : index + 1,
                Note = (stop.Note ?? string.Empty).Trim()
            })
            .ToList();

        if (stopInputs.Count == 0)
            return ([], "Tour phải có ít nhất một điểm dừng.");

        if (stopInputs.Any(x => !x.PoiId.HasValue || x.PoiId.Value <= 0))
            return ([], "Mỗi điểm dừng phải chọn một POI.");

        if (stopInputs.GroupBy(x => x.DisplayOrder).Any(x => x.Count() > 1))
            return ([], "Thứ tự stop không được trùng nhau.");

        var poiIds = stopInputs
            .Where(x => x.PoiId.HasValue)
            .Select(x => x.PoiId!.Value)
            .Distinct()
            .ToList();

        var poiLookup = await _context.Pois
            .AsNoTracking()
            .Where(x => poiIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive, x.ApprovalStatus })
            .ToDictionaryAsync(x => x.Id);

        if (poiLookup.Count != poiIds.Count)
            return ([], "Có POI trong tour không tồn tại.");

        if (vm.IsActive && stopInputs.Any(x =>
                x.PoiId.HasValue &&
                (!poiLookup[x.PoiId.Value].IsActive ||
                 !PoiApprovalStatus.IsApproved(poiLookup[x.PoiId.Value].ApprovalStatus))))
        {
            return ([], "Tour đang hoạt động chỉ được chứa POI đã duyệt và đang hoạt động.");
        }

        var stops = stopInputs
            .OrderBy(x => x.DisplayOrder)
            .Select(x => new TourStop
            {
                PoiId = x.PoiId!.Value,
                DisplayOrder = x.DisplayOrder,
                Note = x.Note
            })
            .ToList();

        return (stops, null);
    }

    private static void MapToEntity(Tour entity, TourFormViewModel vm, IReadOnlyList<TourStop> stops)
    {
        entity.Name = (vm.Name ?? string.Empty).Trim();
        entity.Description = (vm.Description ?? string.Empty).Trim();
        entity.TtsScriptVi = (vm.TtsScriptVi ?? string.Empty).Trim();
        entity.TtsScriptEn = (vm.TtsScriptEn ?? string.Empty).Trim();
        entity.TtsScriptZh = (vm.TtsScriptZh ?? string.Empty).Trim();
        entity.TtsScriptJa = (vm.TtsScriptJa ?? string.Empty).Trim();
        entity.TtsScriptDe = (vm.TtsScriptDe ?? string.Empty).Trim();
        entity.IsActive = vm.IsActive;

        entity.Stops.Clear();
        foreach (var stop in stops)
        {
            entity.Stops.Add(new TourStop
            {
                PoiId = stop.PoiId,
                DisplayOrder = stop.DisplayOrder,
                Note = stop.Note
            });
        }
    }

    private async Task PopulateGeneratedFieldsAsync(TourFormViewModel vm)
    {
        var translations = await _ttsTranslationService.GenerateFromVietnameseAsync(vm.TtsScriptVi ?? string.Empty);
        vm.TtsScriptVi = translations.Vi;
        vm.TtsScriptEn = translations.En;
        vm.TtsScriptZh = translations.Zh;
        vm.TtsScriptJa = translations.Ja;
        vm.TtsScriptDe = translations.De;
    }

    private IQueryable<Tour> BuildTourQuery()
        => _context.Tours
            .Include(x => x.Stops.OrderBy(stop => stop.DisplayOrder))
            .ThenInclude(x => x.Poi)
            .ThenInclude(x => x!.Translations)
            .Include(x => x.Stops.OrderBy(stop => stop.DisplayOrder))
            .ThenInclude(x => x.Poi)
            .ThenInclude(x => x!.AudioAssets);

    private async Task<List<SelectListItem>> GetPoiOptionsAsync()
    {
        return await _context.Pois
            .AsNoTracking()
            .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
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
