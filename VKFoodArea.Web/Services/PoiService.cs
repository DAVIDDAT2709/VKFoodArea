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
    private static readonly string[] SupportedTranslationLanguages = ["vi", "en", "zh", "ja", "de"];
    private static readonly string[] SupportedAudioLanguages = ["vi", "en", "ja"];

    private readonly AppDbContext _context;
    private readonly ITtsTranslationService _ttsTranslationService;
    private readonly IPoiImageStorageService _poiImageStorageService;
    private readonly IPoiAudioStorageService _poiAudioStorageService;

    public PoiService(
        AppDbContext context,
        ITtsTranslationService ttsTranslationService,
        IPoiImageStorageService poiImageStorageService,
        IPoiAudioStorageService poiAudioStorageService)
    {
        _context = context;
        _ttsTranslationService = ttsTranslationService;
        _poiImageStorageService = poiImageStorageService;
        _poiAudioStorageService = poiAudioStorageService;
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
        var poi = await BuildPoiContentQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
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
        await PopulateStoredAudioAsync(vm);

        var poi = MapToEntity(vm, new Poi());
        SyncContentCollections(poi, vm);

        _context.Pois.Add(poi);
        await _context.SaveChangesAsync();

        return poi.Id;
    }

    public async Task<bool> UpdateAsync(int id, PoiFormViewModel vm)
    {
        var poi = await _context.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return false;

        await PopulateGeneratedFieldsAsync(vm);
        await PopulateStoredImageAsync(vm);
        await PopulateStoredAudioAsync(vm);

        MapToEntity(vm, poi);
        SyncContentCollections(poi, vm);
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

    public string? ValidateAudioFile(IFormFile? audioFile)
        => _poiAudioStorageService.Validate(audioFile);

    public async Task<List<PoiDto>> GetActiveForApiAsync()
    {
        var pois = await BuildPoiContentQuery()
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return pois
            .Select(x => ToDto(x, x.QrCode, "poi-default"))
            .ToList();
    }

    public async Task<PoiDto?> GetByIdForApiAsync(int id)
    {
        var poi = await BuildPoiContentQuery()
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .FirstOrDefaultAsync();

        return poi is null
            ? null
            : ToDto(poi, poi.QrCode, "poi-default");
    }

    public async Task<PoiDto?> GetByQrCodeForApiAsync(string qrCode)
    {
        var normalized = QrCodeHelper.Normalize(qrCode);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var qrItemMatch = await _context.QrCodeItems
            .AsNoTracking()
            .Include(x => x.Poi)
            .ThenInclude(poi => poi!.Translations)
            .Include(x => x.Poi)
            .ThenInclude(poi => poi!.AudioAssets)
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

        var poiMatch = await BuildPoiContentQuery()
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

    private IQueryable<Poi> BuildPoiContentQuery()
        => _context.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets);

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
        TtsScriptVi = GetTranslationScript(poi, "vi", poi.TtsScriptVi),
        TtsScriptEn = GetTranslationScript(poi, "en", poi.TtsScriptEn),
        TtsScriptZh = GetTranslationScript(poi, "zh", poi.TtsScriptZh),
        TtsScriptJa = GetTranslationScript(poi, "ja", poi.TtsScriptJa),
        TtsScriptDe = GetTranslationScript(poi, "de", poi.TtsScriptDe),
        AudioFileVi = GetAudioFile(poi, "vi", poi.AudioFileVi),
        AudioFileEn = GetAudioFile(poi, "en", poi.AudioFileEn),
        AudioFileJa = GetAudioFile(poi, "ja", poi.AudioFileJa),
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
        poi.AudioFileVi = (vm.AudioFileVi ?? string.Empty).Trim();
        poi.AudioFileEn = (vm.AudioFileEn ?? string.Empty).Trim();
        poi.AudioFileJa = (vm.AudioFileJa ?? string.Empty).Trim();
        poi.QrCode = QrCodeHelper.Normalize(vm.QrCode);
        poi.IsActive = vm.IsActive;
        return poi;
    }

    private static void SyncContentCollections(Poi poi, PoiFormViewModel vm)
    {
        var translations = new Dictionary<string, string?>
        {
            ["vi"] = vm.TtsScriptVi,
            ["en"] = vm.TtsScriptEn,
            ["zh"] = vm.TtsScriptZh,
            ["ja"] = vm.TtsScriptJa,
            ["de"] = vm.TtsScriptDe
        };

        foreach (var language in SupportedTranslationLanguages)
            UpsertTranslation(poi, language, translations[language]);

        var audioAssets = new Dictionary<string, string?>
        {
            ["vi"] = vm.AudioFileVi,
            ["en"] = vm.AudioFileEn,
            ["ja"] = vm.AudioFileJa
        };

        foreach (var language in SupportedAudioLanguages)
            UpsertAudioAsset(poi, language, audioAssets[language]);
    }

    private static void UpsertTranslation(Poi poi, string language, string? script)
    {
        var normalizedScript = (script ?? string.Empty).Trim();
        var existing = poi.Translations.FirstOrDefault(x => x.Language == language);

        if (string.IsNullOrWhiteSpace(normalizedScript))
        {
            if (existing is not null)
                poi.Translations.Remove(existing);

            return;
        }

        if (existing is null)
        {
            poi.Translations.Add(new PoiTranslation
            {
                Language = language,
                Script = normalizedScript
            });
            return;
        }

        existing.Script = normalizedScript;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static void UpsertAudioAsset(Poi poi, string language, string? fileUrl)
    {
        var normalizedFileUrl = (fileUrl ?? string.Empty).Trim();
        var existing = poi.AudioAssets.FirstOrDefault(x => x.Language == language);

        if (string.IsNullOrWhiteSpace(normalizedFileUrl))
        {
            if (existing is not null)
                poi.AudioAssets.Remove(existing);

            return;
        }

        if (existing is null)
        {
            poi.AudioAssets.Add(new PoiAudioAsset
            {
                Language = language,
                FileUrl = normalizedFileUrl,
                IsActive = true
            });
            return;
        }

        existing.FileUrl = normalizedFileUrl;
        existing.IsActive = true;
        existing.UpdatedAt = DateTime.UtcNow;
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

    private async Task PopulateStoredAudioAsync(PoiFormViewModel vm)
    {
        vm.AudioFileVi = await SaveAudioOrKeepExistingAsync(vm.AudioFileViUpload, vm.AudioFileVi, vm.Name, "vi");
        vm.AudioFileEn = await SaveAudioOrKeepExistingAsync(vm.AudioFileEnUpload, vm.AudioFileEn, vm.Name, "en");
        vm.AudioFileJa = await SaveAudioOrKeepExistingAsync(vm.AudioFileJaUpload, vm.AudioFileJa, vm.Name, "ja");
    }

    private async Task<string> SaveAudioOrKeepExistingAsync(
        IFormFile? audioFile,
        string? currentValue,
        string poiName,
        string language)
    {
        if (audioFile is null)
            return (currentValue ?? string.Empty).Trim();

        return await _poiAudioStorageService.SaveAsync(audioFile, poiName, language);
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
            TtsScriptVi = GetTranslationScript(poi, "vi", poi.TtsScriptVi),
            TtsScriptEn = GetTranslationScript(poi, "en", poi.TtsScriptEn),
            TtsScriptZh = GetTranslationScript(poi, "zh", poi.TtsScriptZh),
            TtsScriptJa = GetTranslationScript(poi, "ja", poi.TtsScriptJa),
            TtsScriptDe = GetTranslationScript(poi, "de", poi.TtsScriptDe),
            AudioFileVi = GetAudioFile(poi, "vi", poi.AudioFileVi),
            AudioFileEn = GetAudioFile(poi, "en", poi.AudioFileEn),
            AudioFileJa = GetAudioFile(poi, "ja", poi.AudioFileJa),
            MatchedQrCode = matchedQrCode,
            QrSource = qrSource
        };
    }

    private static string GetTranslationScript(Poi poi, string language, string fallback)
        => poi.Translations
               .FirstOrDefault(x => x.Language == language)?
               .Script
               .Trim()
           ?? fallback;

    private static string GetAudioFile(Poi poi, string language, string fallback)
        => poi.AudioAssets
               .FirstOrDefault(x => x.Language == language && x.IsActive)?
               .FileUrl
               .Trim()
           ?? fallback;
}
