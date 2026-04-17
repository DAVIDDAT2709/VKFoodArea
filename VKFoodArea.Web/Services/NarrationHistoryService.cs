using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Helpers;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class NarrationHistoryService : INarrationHistoryService
{
    private readonly AppDbContext _context;
    private readonly ICurrentAdminService _currentAdminService;

    public NarrationHistoryService(AppDbContext context, ICurrentAdminService currentAdminService)
    {
        _context = context;
        _currentAdminService = currentAdminService;
    }

    public async Task<NarrationHistoryIndexViewModel> GetIndexAsync(
        string? query,
        DateTime? fromDate,
        DateTime? toDate,
        string? language,
        string? mode,
        string? source)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        var normalizedLanguage = NormalizeSimpleFilter(language);
        var normalizedMode = NormalizeSimpleFilter(mode);
        var normalizedSource = NormalizeTriggerSource(source);

        var dataQuery = BuildFilteredQuery(normalizedSource);

        if (!string.IsNullOrWhiteSpace(normalizedLanguage))
            dataQuery = dataQuery.Where(x => x.Language == normalizedLanguage);

        if (!string.IsNullOrWhiteSpace(normalizedMode))
            dataQuery = dataQuery.Where(x => x.Mode == normalizedMode);

        if (fromDate.HasValue)
        {
            var from = WebDisplayTime.ToUtcStartOfDay(fromDate.Value);
            dataQuery = dataQuery.Where(x => x.PlayedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toExclusive = WebDisplayTime.ToUtcStartOfNextDay(toDate.Value);
            dataQuery = dataQuery.Where(x => x.PlayedAt < toExclusive);
        }

        var items = await dataQuery.ToListAsync();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            items = items
                .Where(x => MatchesSearch(x.PoiName, normalizedQuery))
                .ToList();
        }

        return new NarrationHistoryIndexViewModel
        {
            Query = normalizedQuery,
            FromDate = fromDate,
            ToDate = toDate,
            Language = normalizedLanguage,
            Mode = normalizedMode,
            Source = normalizedSource,
            Items = items,
            TodayCount = items.Count(x => x.PlayedAt >= WebDisplayTime.TodayStartUtc &&
                                          x.PlayedAt < WebDisplayTime.TomorrowStartUtc),
            GpsCount = items.Count(x => x.TriggerSource == "gps" || x.TriggerSource == "auto"),
            QrCount = items.Count(x => x.TriggerSource == "qr"),
            ManualCount = items.Count(x => x.TriggerSource == "manual"),
            TopPois = items
                .GroupBy(x => new { x.PoiId, x.PoiName })
                .Select(x => new TopPoiPerformanceViewModel
                {
                    PoiId = x.Key.PoiId,
                    PoiName = x.Key.PoiName,
                    Count = x.Count(),
                    LatestPlayedAt = x.Max(item => item.PlayedAt)
                })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.PoiName)
                .Take(5)
                .ToList()
        };
    }

    public async Task<List<NarrationHistory>> GetAllAsync(string? source)
    {
        return await BuildFilteredQuery(source).ToListAsync();
    }

    public async Task<List<NarrationHistoryApiViewModel>> GetRecentForApiAsync(string? source, string? userKey, int top = 100)
    {
        var normalizedTop = Math.Clamp(top, 1, 200);

        return await BuildFilteredQuery(source, userKey)
            .Take(normalizedTop)
            .Select(x => new NarrationHistoryApiViewModel
            {
                Id = x.Id,
                PoiId = x.PoiId,
                PoiName = x.PoiName,
                UserKey = x.UserKey,
                Language = x.Language,
                TriggerSource = x.TriggerSource,
                Mode = x.Mode,
                PlayedAt = x.PlayedAt,
                DurationSeconds = x.DurationSeconds,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            })
            .ToListAsync();
    }

    public async Task<NarrationHistoryApiViewModel?> CreateFromAppAsync(NarrationHistoryCreateApiViewModel vm)
    {
        var language = NormalizeSimpleFilter(vm.Language) ?? "vi";
        var triggerSource = NormalizeTriggerSource(vm.TriggerSource);
        var mode = NormalizeSimpleFilter(vm.Mode) ?? "tts";
        var poi = await ResolvePoiAsync(vm);

        if (poi is null)
            return null;

        var entity = new NarrationHistory
        {
            PoiId = poi.Id,
            PoiName = poi.Name,
            UserKey = NormalizeUserKey(vm.UserKey),
            Language = language,
            TriggerSource = triggerSource,
            Mode = mode,
            PlayedAt = NormalizePlayedAt(vm.PlayedAt),
            DurationSeconds = vm.DurationSeconds,
            Latitude = vm.Latitude,
            Longitude = vm.Longitude
        };

        _context.NarrationHistories.Add(entity);
        await _context.SaveChangesAsync();

        return new NarrationHistoryApiViewModel
        {
            Id = entity.Id,
            PoiId = entity.PoiId,
            PoiName = entity.PoiName,
            UserKey = entity.UserKey,
            Language = entity.Language,
            TriggerSource = entity.TriggerSource,
            Mode = entity.Mode,
            PlayedAt = entity.PlayedAt,
            DurationSeconds = entity.DurationSeconds,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude
        };
    }

    public async Task<NarrationHistoryApiViewModel?> GetByIdForApiAsync(int id)
    {
        return await _context.NarrationHistories
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new NarrationHistoryApiViewModel
            {
                Id = x.Id,
                PoiId = x.PoiId,
                PoiName = x.PoiName,
                UserKey = x.UserKey,
                Language = x.Language,
                TriggerSource = x.TriggerSource,
                Mode = x.Mode,
                PlayedAt = x.PlayedAt,
                DurationSeconds = x.DurationSeconds,
                Latitude = x.Latitude,
                Longitude = x.Longitude
            })
            .FirstOrDefaultAsync();
    }

    public async Task<int> ClearForApiAsync(string? userKey, string? source)
    {
        var normalizedUserKey = NormalizeUserKey(userKey);
        if (string.IsNullOrWhiteSpace(normalizedUserKey))
            return 0;

        var items = await BuildFilteredQuery(source, normalizedUserKey).ToListAsync();
        if (items.Count == 0)
            return 0;

        _context.NarrationHistories.RemoveRange(items);
        await _context.SaveChangesAsync();
        return items.Count;
    }

    private IQueryable<NarrationHistory> BuildFilteredQuery(string? source, string? userKey = null)
    {
        var normalizedSource = NormalizeTriggerSource(source);
        var normalizedUserKey = NormalizeUserKey(userKey);

        var query = _context.NarrationHistories
            .Include(x => x.Poi)
            .AsNoTracking()
            .OrderByDescending(x => x.PlayedAt)
            .ThenByDescending(x => x.Id)
            .AsQueryable();

        if (_currentAdminService.IsRestaurantOwner)
        {
            query = _currentAdminService.UserId.HasValue
                ? query.Where(x => x.Poi != null && x.Poi.OwnerAdminUserId == _currentAdminService.UserId.Value)
                : query.Where(x => false);
        }

        if (!string.IsNullOrWhiteSpace(normalizedUserKey))
            query = query.Where(x => x.UserKey == normalizedUserKey);

        if (string.IsNullOrWhiteSpace(normalizedSource))
            return query;

        if (normalizedSource == "gps")
        {
            return query.Where(x =>
                x.TriggerSource == "gps" ||
                x.TriggerSource == "auto");
        }

        return query.Where(x => x.TriggerSource == normalizedSource);
    }

    private async Task<Poi?> ResolvePoiAsync(NarrationHistoryCreateApiViewModel vm)
    {
        var normalizedQrCode = QrCodeHelper.Normalize(vm.QrCode);
        if (!string.IsNullOrWhiteSpace(normalizedQrCode))
        {
            var poiByQr = await _context.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    !string.IsNullOrWhiteSpace(x.QrCode) &&
                    x.QrCode.ToLower() == normalizedQrCode);

            if (poiByQr is not null)
                return poiByQr;
        }

        var normalizedPoiName = (vm.PoiName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPoiName))
        {
            var poiByName = await _context.Pois
                .AsNoTracking()
                .Where(x => x.IsActive)
                .FirstOrDefaultAsync(x => x.Name == normalizedPoiName);

            if (poiByName is not null)
                return poiByName;
        }

        if (vm.PoiId > 0)
        {
            var poiById = await _context.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == vm.PoiId && x.IsActive);

            if (poiById is not null)
                return poiById;
        }

        return null;
    }

    private static bool MatchesSearch(string? source, string query)
    {
        var normalizedSource = NormalizeSearchText(source);
        var normalizedQuery = NormalizeSearchText(query);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;

        return normalizedSource.Contains(normalizedQuery, StringComparison.Ordinal);
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

    private static string NormalizeTriggerSource(string? source)
    {
        var normalized = (source ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "auto" => "gps",
            "gps" => "gps",
            "tour" => "tour",
            "qr" => "qr",
            "manual" => "manual",
            _ => string.Empty
        };
    }

    private static DateTime NormalizePlayedAt(DateTime? playedAt)
    {
        var now = DateTime.UtcNow;
        if (!playedAt.HasValue)
            return now;

        var utc = playedAt.Value.Kind == DateTimeKind.Utc
            ? playedAt.Value
            : DateTime.SpecifyKind(playedAt.Value, DateTimeKind.Utc);

        if (utc > now.AddMinutes(5))
            return now;

        return utc;
    }

    private static string NormalizeUserKey(string? userKey)
        => (userKey ?? string.Empty).Trim().ToLowerInvariant();

    private static string? NormalizeSimpleFilter(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
