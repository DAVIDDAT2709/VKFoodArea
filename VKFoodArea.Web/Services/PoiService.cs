using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Dtos;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class PoiService : IPoiService
{
    private readonly AppDbContext _context;
    private readonly ITtsTranslationService _ttsTranslationService;
    private readonly IPoiImageStorageService _poiImageStorageService;

    public PoiService(
        AppDbContext context,
        ITtsTranslationService ttsTranslationService,
        IPoiImageStorageService poiImageStorageService)
    {
        _context = context;
        _ttsTranslationService = ttsTranslationService;
        _poiImageStorageService = poiImageStorageService;
    }

    public async Task<List<Poi>> GetAllAsync()
    {
        return await _context.Pois
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<PoiFormViewModel?> GetEditFormAsync(int id)
    {
        var poi = await _context.Pois.FindAsync(id);
        if (poi is null)
            return null;

        return MapToViewModel(poi);
    }

    public async Task<Poi?> GetDeleteModelAsync(int id)
    {
        return await _context.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(PoiFormViewModel vm)
    {
        await PopulateGeneratedFieldsAsync(vm);
        await PopulateStoredImageAsync(vm);

        var poi = MapToEntity(vm, new Poi());
        _context.Pois.Add(poi);
        await _context.SaveChangesAsync();

        return poi.Id;
    }

    public async Task<bool> UpdateAsync(int id, PoiFormViewModel vm)
    {
        var poi = await _context.Pois.FindAsync(id);
        if (poi is null)
            return false;

        await PopulateGeneratedFieldsAsync(vm);
        await PopulateStoredImageAsync(vm);

        MapToEntity(vm, poi);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var poi = await _context.Pois.FindAsync(id);
        if (poi is null)
            return false;

        _context.Pois.Remove(poi);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string?> ValidateDefaultQrCodeAsync(int? currentPoiId, string? qrCode)
    {
        var normalized = QrCodeHelper.Normalize(qrCode);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var duplicatedInPois = await _context.Pois
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != currentPoiId &&
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == normalized);

        if (duplicatedInPois)
            return "Mã QR mặc định bị trùng với một POI khác.";

        var duplicatedInQrItems = await _context.QrCodeItems
            .AsNoTracking()
            .AnyAsync(x =>
                !string.IsNullOrWhiteSpace(x.Code) &&
                x.Code.ToLower() == normalized);

        if (duplicatedInQrItems)
            return "Mã QR mặc định bị trùng với một QR Code đã tạo trong module QR.";

        return null;
    }

    public string? ValidateImageFile(IFormFile? imageFile)
        => _poiImageStorageService.Validate(imageFile);

    public async Task<List<PoiDto>> GetActiveForApiAsync()
    {
        return await _context.Pois
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => ToDto(x, x.QrCode, "poi-default"))
            .ToListAsync();
    }

    public async Task<PoiDto?> GetByIdForApiAsync(int id)
    {
        return await _context.Pois
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => ToDto(x, x.QrCode, "poi-default"))
            .FirstOrDefaultAsync();
    }

    public async Task<PoiDto?> GetByQrCodeForApiAsync(string qrCode)
    {
        var normalized = QrCodeHelper.Normalize(qrCode);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var qrItemMatch = await _context.QrCodeItems
            .AsNoTracking()
            .Include(x => x.Poi)
            .Where(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.Code) &&
                x.Code.ToLower() == normalized &&
                x.Poi != null &&
                x.Poi.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (qrItemMatch?.Poi is not null)
            return ToDto(qrItemMatch.Poi, qrItemMatch.Code, "qr-item");

        var poiMatch = await _context.Pois
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                !string.IsNullOrWhiteSpace(x.QrCode) &&
                x.QrCode.ToLower() == normalized)
            .FirstOrDefaultAsync();

        if (poiMatch is null)
            return null;

        return ToDto(poiMatch, poiMatch.QrCode, "poi-default");
    }

    private static PoiFormViewModel MapToViewModel(Poi poi) => new()
    {
        Id = poi.Id,
        Name = poi.Name,
        Address = poi.Address,
        PhoneNumber = poi.PhoneNumber,
        ImageUrl = poi.ImageUrl,
        Latitude = poi.Latitude,
        Longitude = poi.Longitude,
        RadiusMeters = poi.RadiusMeters,
        Priority = poi.Priority,
        TtsScriptVi = poi.TtsScriptVi,
        TtsScriptEn = poi.TtsScriptEn,
        TtsScriptZh = poi.TtsScriptZh,
        TtsScriptJa = poi.TtsScriptJa,
        TtsScriptDe = poi.TtsScriptDe,
        QrCode = poi.QrCode,
        IsActive = poi.IsActive
    };

    private static Poi MapToEntity(PoiFormViewModel vm, Poi poi)
    {
        poi.Name = vm.Name.Trim();
        poi.Address = vm.Address.Trim();
        poi.PhoneNumber = (vm.PhoneNumber ?? string.Empty).Trim();
        poi.ImageUrl = (vm.ImageUrl ?? string.Empty).Trim();
        poi.Latitude = vm.Latitude;
        poi.Longitude = vm.Longitude;
        poi.RadiusMeters = vm.RadiusMeters;
        poi.Priority = vm.Priority;
        poi.Description = BuildShortDescription(vm.TtsScriptVi);
        poi.TtsScriptVi = vm.TtsScriptVi.Trim();
        poi.TtsScriptEn = (vm.TtsScriptEn ?? string.Empty).Trim();
        poi.TtsScriptZh = (vm.TtsScriptZh ?? string.Empty).Trim();
        poi.TtsScriptJa = (vm.TtsScriptJa ?? string.Empty).Trim();
        poi.TtsScriptDe = (vm.TtsScriptDe ?? string.Empty).Trim();
        poi.QrCode = QrCodeHelper.Normalize(vm.QrCode);
        poi.IsActive = vm.IsActive;
        return poi;
    }

    private async Task PopulateGeneratedFieldsAsync(PoiFormViewModel vm)
    {
        var scripts = await _ttsTranslationService.GenerateFromVietnameseAsync(vm.TtsScriptVi);

        vm.TtsScriptVi = scripts.Vi;
        vm.TtsScriptEn = scripts.En;
        vm.TtsScriptZh = scripts.Zh;
        vm.TtsScriptJa = scripts.Ja;
        vm.TtsScriptDe = scripts.De;
    }

    private async Task PopulateStoredImageAsync(PoiFormViewModel vm)
    {
        if (vm.ImageFile is null)
        {
            vm.ImageUrl = (vm.ImageUrl ?? string.Empty).Trim();
            return;
        }

        vm.ImageUrl = await _poiImageStorageService.SaveAsync(vm.ImageFile, vm.Name);
    }

    private static string BuildShortDescription(string? vietnameseTts)
    {
        var normalized = (vietnameseTts ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var sentenceEnd = normalized.IndexOfAny(['.', '!', '?']);
        if (sentenceEnd >= 0)
        {
            var firstSentence = normalized[..(sentenceEnd + 1)].Trim();
            if (firstSentence.Length <= 180)
                return firstSentence;
        }

        const int maxLength = 180;
        if (normalized.Length <= maxLength)
            return normalized;

        var shortened = normalized[..maxLength].TrimEnd();
        var lastSpace = shortened.LastIndexOf(' ');

        if (lastSpace > 80)
            shortened = shortened[..lastSpace];

        return $"{shortened}...";
    }

    private static PoiDto ToDto(Poi poi, string matchedQrCode, string qrSource)
    {
        return new PoiDto
        {
            Id = poi.Id,
            Name = poi.Name,
            Address = poi.Address,
            PhoneNumber = poi.PhoneNumber,
            ImageUrl = poi.ImageUrl,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            RadiusMeters = poi.RadiusMeters,
            Priority = poi.Priority,
            QrCode = poi.QrCode,
            IsActive = poi.IsActive,
            TtsScriptVi = poi.TtsScriptVi,
            TtsScriptEn = poi.TtsScriptEn,
            TtsScriptZh = poi.TtsScriptZh,
            TtsScriptJa = poi.TtsScriptJa,
            TtsScriptDe = poi.TtsScriptDe,
            MatchedQrCode = matchedQrCode,
            QrSource = qrSource
        };
    }
}
