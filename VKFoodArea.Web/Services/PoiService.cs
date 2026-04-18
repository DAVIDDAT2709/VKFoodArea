using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    private readonly ICurrentAdminService _currentAdminService;

    public PoiService(
        AppDbContext context,
        ITtsTranslationService ttsTranslationService,
        IPoiImageStorageService poiImageStorageService,
        IPoiAudioStorageService poiAudioStorageService,
        ICurrentAdminService currentAdminService)
    {
        _context = context;
        _ttsTranslationService = ttsTranslationService;
        _poiImageStorageService = poiImageStorageService;
        _poiAudioStorageService = poiAudioStorageService;
        _currentAdminService = currentAdminService;
    }

    public async Task<PoiFormViewModel> BuildCreateFormAsync()
    {
        var vm = new PoiFormViewModel
        {
            Latitude = 10.7618,
            Longitude = 106.7022,
            RadiusMeters = 30,
            Priority = 1,
            IsActive = _currentAdminService.IsAdmin,
            ApprovalStatus = _currentAdminService.IsAdmin
                ? PoiApprovalStatus.Approved
                : PoiApprovalStatus.Pending
        };

        ApplyFormPermissions(vm);
        await PopulateOwnerOptionsAsync(vm);
        return vm;
    }

    public async Task<PoiFormViewModel> RebuildFormAsync(PoiFormViewModel vm)
    {
        vm.ApprovalStatus = PoiApprovalStatus.Normalize(vm.ApprovalStatus);
        ApplyFormPermissions(vm);
        await PopulateOwnerOptionsAsync(vm);
        return vm;
    }

    public async Task<List<Poi>> GetAllAsync()
    {
        return await ApplyAccessFilter(_context.Pois)
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<PoiIndexViewModel> GetIndexAsync(string? query, string? approvalStatus)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        var normalizedApprovalStatus = NormalizeApprovalStatusFilter(approvalStatus);
        var accessiblePois = await ApplyAccessFilter(_context.Pois)
            .AsNoTracking()
            .Include(x => x.OwnerAdminUser)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var filteredPois = accessiblePois;

        if (!string.IsNullOrWhiteSpace(normalizedApprovalStatus))
        {
            filteredPois = filteredPois
                .Where(x => PoiApprovalStatus.Normalize(x.ApprovalStatus) == normalizedApprovalStatus)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            filteredPois = filteredPois
                .Where(x => MatchesSearch(x, normalizedQuery))
                .ToList();
        }

        return new PoiIndexViewModel
        {
            Query = normalizedQuery,
            ApprovalStatus = normalizedApprovalStatus,
            IsAdmin = _currentAdminService.IsAdmin,
            Items = filteredPois,
            TotalCount = accessiblePois.Count,
            ActiveCount = accessiblePois.Count(x => x.IsActive && PoiApprovalStatus.IsApproved(x.ApprovalStatus)),
            PendingCount = accessiblePois.Count(x => PoiApprovalStatus.Normalize(x.ApprovalStatus) == PoiApprovalStatus.Pending),
            RejectedCount = accessiblePois.Count(x => PoiApprovalStatus.Normalize(x.ApprovalStatus) == PoiApprovalStatus.Rejected),
            ApprovedCount = accessiblePois.Count(x => PoiApprovalStatus.Normalize(x.ApprovalStatus) == PoiApprovalStatus.Approved)
        };
    }

    public async Task<PoiFormViewModel?> GetEditFormAsync(int id)
    {
        var poi = await ApplyAccessFilter(BuildPoiContentQuery())
            .FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return null;

        var vm = MapToViewModel(poi);
        ApplyFormPermissions(vm);
        await PopulateOwnerOptionsAsync(vm);
        return vm;
    }

    public async Task<Poi?> GetDeleteModelAsync(int id)
    {
        return await ApplyAccessFilter(_context.Pois)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<int> CreateAsync(PoiFormViewModel vm)
    {
        await PopulateGeneratedFieldsAsync(vm);
        await PopulateStoredImageAsync(vm);
        await PopulateStoredAudioAsync(vm);

        var poi = MapToEntity(vm, new Poi());
        ApplyOwner(vm, poi, isNew: true);
        ApplyApprovalForCreate(poi);
        SyncContentCollections(poi, vm);

        _context.Pois.Add(poi);
        await _context.SaveChangesAsync();

        return poi.Id;
    }

    public async Task<bool> UpdateAsync(int id, PoiFormViewModel vm)
    {
        var poi = await ApplyAccessFilter(_context.Pois)
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return false;

        var originalApprovalStatus = PoiApprovalStatus.Normalize(poi.ApprovalStatus);
        var originalIsActive = poi.IsActive;

        await PopulateGeneratedFieldsAsync(vm);
        await PopulateStoredImageAsync(vm);
        await PopulateStoredAudioAsync(vm);

        MapToEntity(vm, poi);
        ApplyOwner(vm, poi, isNew: false);
        ApplyApprovalForUpdate(poi, originalApprovalStatus, originalIsActive);
        SyncContentCollections(poi, vm);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var poi = await ApplyAccessFilter(_context.Pois)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return false;

        _context.Pois.Remove(poi);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveAsync(int id)
    {
        if (!_currentAdminService.IsAdmin)
            return false;

        var poi = await _context.Pois.FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return false;

        poi.ApprovalStatus = PoiApprovalStatus.Approved;
        poi.IsActive = true;
        poi.ReviewedAt = DateTime.UtcNow;
        poi.ReviewedByAdminUserId = _currentAdminService.UserId;
        poi.ReviewNote = string.Empty;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(int id)
    {
        if (!_currentAdminService.IsAdmin)
            return false;

        var poi = await _context.Pois.FirstOrDefaultAsync(x => x.Id == id);
        if (poi is null)
            return false;

        poi.ApprovalStatus = PoiApprovalStatus.Rejected;
        poi.IsActive = false;
        poi.ReviewedAt = DateTime.UtcNow;
        poi.ReviewedByAdminUserId = _currentAdminService.UserId;
        poi.ReviewNote = "Admin từ chối POI.";

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
            .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return pois
            .Select(x => ApiDtoMapper.ToPoiDto(x, x.QrCode, "poi-default"))
            .ToList();
    }

    public async Task<PoiDto?> GetByIdForApiAsync(int id)
    {
        var poi = await BuildPoiContentQuery()
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
            .FirstOrDefaultAsync();

        return poi is null
            ? null
            : ApiDtoMapper.ToPoiDto(poi, poi.QrCode, "poi-default");
    }

    private IQueryable<Poi> BuildPoiContentQuery()
        => _context.Pois
            .Include(x => x.Translations)
            .Include(x => x.AudioAssets);

    private IQueryable<Poi> ApplyAccessFilter(IQueryable<Poi> query)
    {
        if (_currentAdminService.IsAdmin)
            return query;

        return _currentAdminService.UserId.HasValue
            ? query.Where(x => x.OwnerAdminUserId == _currentAdminService.UserId.Value)
            : query.Where(x => false);
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
        TtsScriptVi = GetTranslationScript(poi, "vi", poi.TtsScriptVi),
        TtsScriptEn = GetTranslationScript(poi, "en", poi.TtsScriptEn),
        TtsScriptZh = GetTranslationScript(poi, "zh", poi.TtsScriptZh),
        TtsScriptJa = GetTranslationScript(poi, "ja", poi.TtsScriptJa),
        TtsScriptDe = GetTranslationScript(poi, "de", poi.TtsScriptDe),
        AudioFileVi = GetAudioFile(poi, "vi", poi.AudioFileVi),
        AudioFileEn = GetAudioFile(poi, "en", poi.AudioFileEn),
        AudioFileJa = GetAudioFile(poi, "ja", poi.AudioFileJa),
        QrCode = poi.QrCode,
        IsActive = poi.IsActive,
        ApprovalStatus = PoiApprovalStatus.Normalize(poi.ApprovalStatus),
        OwnerAdminUserId = poi.OwnerAdminUserId
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
        poi.ApprovalStatus = PoiApprovalStatus.Normalize(vm.ApprovalStatus);
        return poi;
    }

    private void ApplyFormPermissions(PoiFormViewModel vm)
    {
        vm.CanEditActiveState = _currentAdminService.IsAdmin;

        if (!_currentAdminService.IsAdmin)
            vm.IsActive = false;
    }

    private void ApplyOwner(PoiFormViewModel vm, Poi poi, bool isNew)
    {
        if (_currentAdminService.IsAdmin)
        {
            poi.OwnerAdminUserId = vm.OwnerAdminUserId;
            return;
        }

        if (_currentAdminService.IsRestaurantOwner && _currentAdminService.UserId.HasValue)
            poi.OwnerAdminUserId = _currentAdminService.UserId.Value;
        else if (isNew)
            poi.OwnerAdminUserId = null;
    }

    private void ApplyApprovalForCreate(Poi poi)
    {
        if (_currentAdminService.IsAdmin)
        {
            poi.ApprovalStatus = PoiApprovalStatus.Approved;
            poi.SubmittedAt = DateTime.UtcNow;
            poi.ReviewedAt = DateTime.UtcNow;
            poi.ReviewedByAdminUserId = _currentAdminService.UserId;
            return;
        }

        poi.ApprovalStatus = PoiApprovalStatus.Pending;
        poi.IsActive = false;
        poi.SubmittedAt = DateTime.UtcNow;
        poi.ReviewedAt = null;
        poi.ReviewedByAdminUserId = null;
        poi.ReviewNote = string.Empty;
    }

    private void ApplyApprovalForUpdate(Poi poi, string originalApprovalStatus, bool originalIsActive)
    {
        if (_currentAdminService.IsAdmin)
        {
            poi.ApprovalStatus = PoiApprovalStatus.Normalize(poi.ApprovalStatus);
            if (poi.ApprovalStatus != PoiApprovalStatus.Approved)
                poi.IsActive = false;

            return;
        }

        poi.ApprovalStatus = originalApprovalStatus;
        poi.IsActive = originalApprovalStatus == PoiApprovalStatus.Approved && originalIsActive;

        if (originalApprovalStatus == PoiApprovalStatus.Pending)
            poi.SubmittedAt ??= DateTime.UtcNow;
    }

    private async Task PopulateOwnerOptionsAsync(PoiFormViewModel vm)
    {
        vm.CanAssignOwner = _currentAdminService.IsAdmin;
        vm.OwnerOptions.Clear();

        if (!vm.CanAssignOwner)
            return;

        vm.OwnerOptions.Add(new SelectListItem("Chưa gán chủ quán", string.Empty, !vm.OwnerAdminUserId.HasValue));

        var owners = await _context.AdminUsers
            .AsNoTracking()
            .Where(x => x.IsActive && x.Role == AdminRoleNames.RestaurantOwner)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Username)
            .ToListAsync();

        foreach (var owner in owners)
        {
            vm.OwnerOptions.Add(new SelectListItem(
                $"{owner.FullName} ({owner.Username})",
                owner.Id.ToString(),
                vm.OwnerAdminUserId == owner.Id));
        }
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

    private static string NormalizeApprovalStatusFilter(string? status)
    {
        var normalized = (status ?? string.Empty).Trim();
        return normalized.Equals(PoiApprovalStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(PoiApprovalStatus.Approved, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(PoiApprovalStatus.Rejected, StringComparison.OrdinalIgnoreCase)
            ? PoiApprovalStatus.Normalize(normalized)
            : string.Empty;
    }

    private static bool MatchesSearch(Poi poi, string query)
    {
        var normalizedQuery = NormalizeSearchText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;

        var searchable = NormalizeSearchText(string.Join(
            " ",
            poi.Name,
            poi.Address,
            poi.PhoneNumber,
            poi.QrCode,
            poi.Description));

        return normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token => searchable.Contains(token, StringComparison.Ordinal));
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value
                     .Trim()
                     .ToLowerInvariant()
                     .Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var normalizedCharacter = character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => character
            };

            if (char.IsWhiteSpace(normalizedCharacter))
            {
                if (previousWasSpace)
                    continue;

                builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(normalizedCharacter);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }
}
