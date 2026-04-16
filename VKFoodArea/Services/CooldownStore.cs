namespace VKFoodArea.Services;

public class CooldownStore
{
    private readonly Dictionary<int, DateTimeOffset> _poiPlayedAt = new();
    private readonly Dictionary<int, DateTimeOffset> _insideSince = new();
    private DateTimeOffset? _globalPlayedAt;
    private int? _lastPlayedPoiId;

    public void MarkInside(int poiId)
    {
        if (!_insideSince.ContainsKey(poiId))
            _insideSince[poiId] = DateTimeOffset.UtcNow;
    }

    public void MarkOutside(int poiId)
    {
        _insideSince.Remove(poiId);
    }

    public bool PassedDebounce(int poiId, TimeSpan debounce)
    {
        if (!_insideSince.TryGetValue(poiId, out var since))
            return false;

        return DateTimeOffset.UtcNow - since >= debounce;
    }

    public bool IsPoiCooldownActive(int poiId, TimeSpan cooldown)
    {
        if (!_poiPlayedAt.TryGetValue(poiId, out var at))
            return false;

        return DateTimeOffset.UtcNow - at < cooldown;
    }

    public bool IsGlobalCooldownActive(TimeSpan cooldown, int poiId)
    {
        if (!_globalPlayedAt.HasValue)
            return false;

        // Nếu đang chuyển sang POI khác thì không chặn
        if (_lastPlayedPoiId.HasValue && _lastPlayedPoiId.Value != poiId)
            return false;

        return DateTimeOffset.UtcNow - _globalPlayedAt.Value < cooldown;
    }

    public void MarkPlayed(int poiId)
    {
        var now = DateTimeOffset.UtcNow;
        _poiPlayedAt[poiId] = now;
        _globalPlayedAt = now;
        _lastPlayedPoiId = poiId;
    }
}