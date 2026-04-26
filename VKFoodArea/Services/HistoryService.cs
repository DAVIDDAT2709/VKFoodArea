using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;
using VKFoodArea.Models;
using VKFoodArea.Repositories;

namespace VKFoodArea.Services;

public class HistoryService
{
    private readonly AppDbContext _db;
    private readonly NarrationSyncService _narrationSyncService;
    private readonly PoiRepository _poiRepository;
    private readonly Dictionary<int, HistoryRecord> _recordCache = new();

    public HistoryService(
        AppDbContext db,
        NarrationSyncService narrationSyncService,
        PoiRepository poiRepository)
    {
        _db = db;
        _narrationSyncService = narrationSyncService;
        _poiRepository = poiRepository;
    }

    public async Task<HistoryLoadResult> GetListeningHistoryAsync(
        int? userId,
        string? userKey,
        CancellationToken ct = default)
    {
        var localRows = await LoadLocalRowsAsync(userId, ct);
        List<HistoryRecord>? remoteRows = null;
        var remoteAvailable = false;

        if (!string.IsNullOrWhiteSpace(userKey))
        {
            try
            {
                var remoteItems = await _narrationSyncService.GetRecentHistoryAsync(userKey: userKey, top: 100, ct: ct);
                remoteRows = remoteItems.Select(MapRemoteRow).ToList();
                remoteAvailable = true;
            }
            catch
            {
                remoteRows = null;
            }
        }

        var mergedRows = MergeRows(localRows, remoteRows)
            .OrderByDescending(x => x.PlayedAtUtc)
            .Take(100)
            .ToList();

        _recordCache.Clear();
        foreach (var row in mergedRows)
            _recordCache[row.Id] = row;

        return new HistoryLoadResult(
            mergedRows,
            localRows.Count,
            remoteRows?.Count ?? 0,
            remoteAvailable);
    }

    public async Task<HistoryPlaybackDetail?> GetHistoryDetailAsync(
        int historyId,
        int? userId,
        CancellationToken ct = default)
    {
        if (_recordCache.TryGetValue(historyId, out var cachedRecord))
        {
            return await BuildPlaybackDetailAsync(cachedRecord, ct);
        }

        var logsQuery = await BuildScopedNarrationLogsQueryAsync(userId, ct);
        var log = await logsQuery.FirstOrDefaultAsync(x => x.Id == historyId, ct);

        if (log is null)
            return null;

        var (mode, language) = ParseModeValues(log.Mode);
        var poi = await _poiRepository.GetByIdAsync(log.PoiId, ct);
        var poiName = poi?.Name ?? $"POI #{log.PoiId}";

        var record = new HistoryRecord(
            log.Id,
            log.PoiId,
            poiName,
            log.TourId,
            (log.TourName ?? string.Empty).Trim(),
            log.PlayedAt.UtcDateTime,
            mode,
            language,
            NormalizeTriggerSource(log.TriggerSource),
            "app",
            poi is not null && !string.Equals(mode, "TourIntro", StringComparison.OrdinalIgnoreCase));

        return await BuildPlaybackDetailAsync(record, ct);
    }

    public async Task<HistoryPlaybackSource?> GetPlaybackSourceAsync(
        int historyId,
        int? userId,
        CancellationToken ct = default)
    {
        var detail = await GetHistoryDetailAsync(historyId, userId, ct);
        if (detail is null || !detail.CanReplay || !detail.PoiId.HasValue)
            return null;

        return new HistoryPlaybackSource(
            detail.HistoryId,
            detail.PoiId.Value,
            detail.PoiName,
            detail.Language,
            detail.Mode);
    }

    public async Task ClearHistoryAsync(int? userId, string? userKey, CancellationToken ct = default)
    {
        var logsQuery = await BuildScopedNarrationLogsQueryAsync(userId, ct);
        var logs = await logsQuery.ToListAsync(ct);

        if (logs.Count > 0)
        {
            _db.NarrationLogs.RemoveRange(logs);
            await _db.SaveChangesAsync(ct);
        }

        try
        {
            await _narrationSyncService.ClearHistoryAsync(userKey, ct: ct);
        }
        catch
        {
            // Local clear must still succeed even if the web endpoint is temporarily unavailable.
        }

        _recordCache.Clear();
    }

    private async Task<List<HistoryRecord>> LoadLocalRowsAsync(int? userId, CancellationToken ct)
    {
        var logsQuery = await BuildScopedNarrationLogsQueryAsync(userId, ct);
        var rawRows = await (
            from log in logsQuery
            join poi in _db.Pois.AsNoTracking() on log.PoiId equals poi.Id into poiGroup
            from poi in poiGroup.DefaultIfEmpty()
            select new
            {
                log.Id,
                log.PoiId,
                PoiName = poi != null ? poi.Name : $"POI #{log.PoiId}",
                log.TourId,
                log.TourName,
                log.PlayedAt,
                log.Mode,
                log.TriggerSource,
                HasPoi = poi != null
            })
            .ToListAsync(ct);

        return rawRows
            .OrderByDescending(x => x.PlayedAt)
            .Select(x =>
            {
                var (mode, language) = ParseModeValues(x.Mode);

                return new HistoryRecord(
                    x.Id,
                    x.PoiId,
                    x.PoiName,
                    x.TourId,
                    (x.TourName ?? string.Empty).Trim(),
                    x.PlayedAt.UtcDateTime,
                    mode,
                    language,
                    NormalizeTriggerSource(x.TriggerSource),
                    "app",
                    x.HasPoi && !string.Equals(mode, "TourIntro", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();
    }

    private async Task<IQueryable<NarrationLog>> BuildScopedNarrationLogsQueryAsync(
        int? userId,
        CancellationToken ct)
    {
        var query = _db.NarrationLogs.AsQueryable();
        var hasScopedLogs = await query.AsNoTracking().AnyAsync(x => x.UserId.HasValue, ct);

        if (!userId.HasValue)
            return query.Where(x => x.UserId == null);

        if (!hasScopedLogs)
            return query.Where(_ => true);

        return query.Where(x => x.UserId == userId.Value);
    }

    private static HistoryRecord MapRemoteRow(NarrationHistoryRemoteItem item)
    {
        return new HistoryRecord(
            -Math.Max(item.Id, 1),
            item.PoiId > 0 ? item.PoiId : null,
            item.PoiName,
            item.TourId,
            (item.TourName ?? string.Empty).Trim(),
            DateTime.SpecifyKind(item.PlayedAt, DateTimeKind.Utc),
            NormalizePlaybackMode(item.Mode),
            AppLanguageService.NormalizeLanguage(item.Language),
            NormalizeTriggerSource(item.TriggerSource),
            "web",
            item.PoiId > 0 && !string.Equals(NormalizePlaybackMode(item.Mode), "TourIntro", StringComparison.OrdinalIgnoreCase));
    }

    private static List<HistoryRecord> MergeRows(List<HistoryRecord> localRows, List<HistoryRecord>? remoteRows)
    {
        if (remoteRows is null || remoteRows.Count == 0)
            return localRows;

        var merged = new List<HistoryRecord>(localRows);

        foreach (var remoteRow in remoteRows)
        {
            var duplicateIndex = merged.FindIndex(localRow =>
                string.Equals(localRow.PoiName, remoteRow.PoiName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(localRow.Mode, remoteRow.Mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(localRow.Language, remoteRow.Language, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((localRow.PlayedAtUtc - remoteRow.PlayedAtUtc).TotalSeconds) <= 3);

            if (duplicateIndex < 0)
            {
                merged.Add(remoteRow);
                continue;
            }

            var localRow = merged[duplicateIndex];
            merged[duplicateIndex] = localRow with
            {
                TourId = localRow.TourId ?? remoteRow.TourId,
                TourName = string.IsNullOrWhiteSpace(localRow.TourName) ? remoteRow.TourName : localRow.TourName,
                TriggerSource = string.IsNullOrWhiteSpace(localRow.TriggerSource) ? remoteRow.TriggerSource : localRow.TriggerSource,
                CanReplay = localRow.CanReplay || remoteRow.CanReplay
            };
        }

        return merged;
    }

    private async Task<HistoryPlaybackDetail> BuildPlaybackDetailAsync(
        HistoryRecord record,
        CancellationToken ct)
    {
        var resolvedPoiId = record.PoiId;
        var resolvedPoiName = record.PoiName;
        var canReplay = record.CanReplay;
        var isTourIntro = string.Equals(record.Mode, "TourIntro", StringComparison.OrdinalIgnoreCase);

        if (!canReplay && !isTourIntro)
        {
            var fallbackPoi = await ResolvePoiAsync(record, ct);
            if (fallbackPoi is not null)
            {
                resolvedPoiId = fallbackPoi.Id;
                resolvedPoiName = fallbackPoi.Name;
                canReplay = true;
            }
        }

        return new HistoryPlaybackDetail(
            record.Id,
            resolvedPoiId,
            resolvedPoiName,
            record.TourId,
            record.TourName,
            record.PlayedAtUtc,
            record.Mode,
            record.Language,
            record.TriggerSource,
            record.Origin,
            canReplay);
    }

    private async Task<Poi?> ResolvePoiAsync(HistoryRecord record, CancellationToken ct)
    {
        if (record.PoiId.HasValue)
            return await _poiRepository.GetByIdAsync(record.PoiId.Value, ct);

        var pois = await _poiRepository.GetActiveAsync(ct);
        return pois.FirstOrDefault(x =>
            string.Equals(x.Name, record.PoiName, StringComparison.OrdinalIgnoreCase));
    }

    private static (string Mode, string Language) ParseModeValues(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("TTS", "vi");

        var parts = raw.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mode = parts.Length > 0 ? NormalizePlaybackMode(parts[0]) : "TTS";
        var language = parts.Length > 1
            ? AppLanguageService.NormalizeLanguage(parts[1])
            : "vi";

        return (mode, language);
    }

    private static string NormalizePlaybackMode(string? mode)
    {
        return (mode ?? "TTS").Trim() switch
        {
            "Auto" or "auto" => "Auto",
            "Audio" or "audio" => "Audio",
            "TourIntro" or "tourintro" or "tour-intro" => "TourIntro",
            _ => "TTS"
        };
    }

    private static string NormalizeTriggerSource(string? triggerSource)
    {
        return (triggerSource ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "auto" => "gps",
            "gps" => "gps",
            "tour" => "tour",
            "qr" => "qr",
            _ => "manual"
        };
    }
}

public sealed record HistoryLoadResult(
    IReadOnlyList<HistoryRecord> Records,
    int LocalCount,
    int RemoteCount,
    bool HasRemoteData);

public sealed record HistoryRecord(
    int Id,
    int? PoiId,
    string PoiName,
    int? TourId,
    string TourName,
    DateTime PlayedAtUtc,
    string Mode,
    string Language,
    string TriggerSource,
    string Origin,
    bool CanReplay);

public sealed record HistoryPlaybackDetail(
    int HistoryId,
    int? PoiId,
    string PoiName,
    int? TourId,
    string TourName,
    DateTime PlayedAtUtc,
    string Mode,
    string Language,
    string TriggerSource,
    string Origin,
    bool CanReplay);

public sealed record HistoryPlaybackSource(
    int HistoryId,
    int PoiId,
    string PoiName,
    string Language,
    string Mode);
