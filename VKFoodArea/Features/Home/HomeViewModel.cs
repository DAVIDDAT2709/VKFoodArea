using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class HomeViewModel : INotifyPropertyChanged
{
    private DateTimeOffset _lastNarrationActionAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan NarrationActionDebounce = TimeSpan.FromMilliseconds(800);
    private readonly PoiService _poiService;
    private readonly PoiRuntimeService _poiRuntimeService;
    private readonly NarrationService _narrationService;
    private readonly AppSettingsService _settingsService;
    private readonly PoiSyncService _poiSyncService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _runtimeStateLock = new(1, 1);

    private bool _isInitialized;
    private string _syncSummary = string.Empty;
    private string _currentNarrationTitle = string.Empty;
    private string _currentNarrationSubtitle = string.Empty;
    private string _currentNarrationImageUrl = string.Empty;
    private bool _isNarrationPlaying;
    private bool _showMiniPlayer;
    private bool _showSearchSuggestions;
    private bool _showSearchEmptyState;
    private double _searchSuggestionHeight;
    private TourSession? _activeTourSession;
    private readonly List<Poi> _allPois = [];
    private List<Poi> _defaultPois = [];
    private string _currentSearchKeyword = string.Empty;
    private bool ShouldIgnoreNarrationAction()
{
    var now = DateTimeOffset.UtcNow;

    if (now - _lastNarrationActionAt < NarrationActionDebounce)
        return true;

    _lastNarrationActionAt = now;
    return false;
}

    public ObservableCollection<Poi> NearbyPois { get; } = new();
    public ObservableCollection<Poi> DisplayedPois { get; } = new();
    public ObservableCollection<HomePoiSuggestion> SearchSuggestions { get; } = new();

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new()
    {
        new LanguageOption("Tiếng Việt", "vi"),
        new LanguageOption("English", "en"),
        new LanguageOption("中文", "zh"),
        new LanguageOption("日本語", "ja"),
        new LanguageOption("Deutsch", "de")
    };

    public ObservableCollection<string> OutputModeOptions { get; } = new()
    {
        "Auto",
        "Audio",
        "TTS"
    };

    public IReadOnlyList<Poi> AllPois => _allPois;

    public bool ShowSearchSuggestions
    {
        get => _showSearchSuggestions;
        private set
        {
            _showSearchSuggestions = value;
            OnPropertyChanged();
        }
    }

    public bool ShowSearchEmptyState
    {
        get => _showSearchEmptyState;
        private set
        {
            _showSearchEmptyState = value;
            OnPropertyChanged();
        }
    }

    public double SearchSuggestionHeight
    {
        get => _searchSuggestionHeight;
        private set
        {
            _searchSuggestionHeight = value;
            OnPropertyChanged();
        }
    }

    public string SearchEmptyMessage => _text["Home.SearchEmptyMessage"];

    public ICommand PlayPoiAudioCommand { get; }
    public ICommand StopNarrationCommand { get; }
    public ICommand CloseMiniPlayerCommand { get; }

    public Location DefaultLocation { get; } = new(10.7618, 106.7022);

    private Location _currentLocation = new(10.7618, 106.7022);
    public Location CurrentLocation
    {
        get => _currentLocation;
        set
        {
            _currentLocation = value;
            OnPropertyChanged();
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    private LanguageOption? _selectedLanguage;
    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            _selectedLanguage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NarrationSummary));
        }
    }

    private string _selectedOutputMode = "Auto";
    public string SelectedOutputMode
    {
        get => _selectedOutputMode;
        set
        {
            _selectedOutputMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NarrationSummary));
        }
    }

    public string NarrationSummary =>
        $"{_text["User.Language"]}: {SelectedLanguage?.DisplayName ?? _text.GetLanguageDisplay("vi")} | " +
        $"{_text["Settings.ModeSection"]}: {_text.GetModeDisplay(SelectedOutputMode)}";

    private Poi? _nearestPoi;
    public Poi? NearestPoi
    {
        get => _nearestPoi;
        set
        {
            _nearestPoi = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NearestPoiText));
        }
    }

    public string NearestPoiText =>
        NearestPoi is null
            ? _text["Home.NearestUnknown"]
            : _text.Format("Home.NearestPrefix", NearestPoi.Name);

    public HomeViewModel(
        PoiService poiService,
        PoiRuntimeService poiRuntimeService,
        NarrationService narrationService,
        AppSettingsService settingsService,
        PoiSyncService poiSyncService,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        _poiService = poiService;
        _poiRuntimeService = poiRuntimeService;
        _narrationService = narrationService;
        _settingsService = settingsService;
        _poiSyncService = poiSyncService;
        _text = text;
        _narrationUiState = narrationUiState;

        _poiRuntimeService.StateChanged += OnRuntimeStateChanged;

        PlayPoiAudioCommand = new Command<Poi>(async poi => await PlayPoiAudioAsync(poi));
        StopNarrationCommand = new Command(async () => await StopNarrationAsync());
        CloseMiniPlayerCommand = new Command(async () => await CloseMiniPlayerAsync());

        RefreshLocalizedText();
    }

    public string CurrentNarrationTitle
    {
        get => _currentNarrationTitle;
        set
        {
            _currentNarrationTitle = value;
            OnPropertyChanged();
        }
    }

    public string CurrentNarrationSubtitle
    {
        get => _currentNarrationSubtitle;
        set
        {
            _currentNarrationSubtitle = value;
            OnPropertyChanged();
        }
    }

    public string CurrentNarrationImageUrl
    {
        get => _currentNarrationImageUrl;
        set
        {
            _currentNarrationImageUrl = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNarrationArtwork));
        }
    }

    public bool HasNarrationArtwork => !string.IsNullOrWhiteSpace(CurrentNarrationImageUrl);

    public bool ShowMiniPlayer
    {
        get => _showMiniPlayer;
        set
        {
            _showMiniPlayer = value;
            OnPropertyChanged();
        }
    }

    public bool IsNarrationPlaying
    {
        get => _isNarrationPlaying;
        set
        {
            _isNarrationPlaying = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MiniPlayerActionGlyph));
            OnPropertyChanged(nameof(MiniPlayerStatusText));
            OnPropertyChanged(nameof(MiniPlayerProgress));
            OnPropertyChanged(nameof(MiniPlayerStateTag));
            OnPropertyChanged(nameof(MiniPlayerHintText));
        }
    }

    public string MiniPlayerActionGlyph => IsNarrationPlaying ? "⏸" : "▶";

    public string MiniPlayerStatusText => IsNarrationPlaying
        ? _text["Home.NarrationStatusPlaying"]
        : _text["Home.NarrationStatusReady"];

    public double MiniPlayerProgress => IsNarrationPlaying ? 0.76 : 0.10;

    public string MiniPlayerStateTag => IsNarrationPlaying
        ? _text["Home.NarrationTagLive"]
        : _text["Home.NarrationTagReady"];

    public string MiniPlayerHintText => IsNarrationPlaying
        ? _text["Home.NarrationHintPlaying"]
        : _text["Home.NarrationHintReady"];

    public async Task InitializeAsync()
    {
        LoadNarrationSettings();
        RefreshNarrationState();
        if (!_isInitialized)
        {
            var initialCount = _poiRuntimeService.GetSnapshot().Pois.Count;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText(_text.Format("Status.LoadingLocalAndSyncing", initialCount));
            });
        }

        await _poiRuntimeService.InitializeAsync();
        await SyncFromRuntimeStateAsync(
            refreshSearch: !string.IsNullOrWhiteSpace(_currentSearchKeyword));

        _isInitialized = true;
        _ = RefreshPoisFromWebAsync();
    }

    public async Task RefreshVisiblePoisAsync(
        PoiSyncService.PoiSyncResult? syncResult = null,
        string? detail = null)
    {
        await _poiRuntimeService.ReloadPoisAsync();
        var activePoiCount = _poiRuntimeService.GetSnapshot().Pois.Count;

        if (syncResult is not null)
            _syncSummary = BuildSyncSummary(syncResult, activePoiCount);

        await SyncFromRuntimeStateAsync(
            detail,
            refreshSearch: !string.IsNullOrWhiteSpace(_currentSearchKeyword));
    }

    public async Task<IReadOnlyList<Poi>> GetMapPoisAsync()
    {
        await Task.CompletedTask;
        var pois = _poiRuntimeService.GetSnapshot().Pois;
        return pois.Count == 0 ? NearbyPois.ToList() : pois;
    }

    public async Task<bool> RefreshCurrentLocationAsync()
    {
        var refreshed = await _poiRuntimeService.RefreshCurrentLocationAsync();
        if (!refreshed)
            return false;

        await SyncFromRuntimeStateAsync();
        return true;
    }

    private async void OnRuntimeStateChanged(object? sender, EventArgs e)
    {
        try
        {
            await SyncFromRuntimeStateAsync();
        }
        catch
        {
        }
    }

    private async Task SyncFromRuntimeStateAsync(
        string? detail = null,
        bool refreshSearch = false)
    {
        await _runtimeStateLock.WaitAsync();
        try
        {
            var snapshot = _poiRuntimeService.GetSnapshot();
            var statusDetail = detail ?? ResolveRuntimeStatusDetail(snapshot);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentLocation = snapshot.CurrentLocation;
                _activeTourSession = snapshot.ActiveTourSession;
                ApplyPoiCatalog(snapshot.CurrentLocation, snapshot.Pois, snapshot.ActiveTourSession);
                NearestPoi = snapshot.NearestPoi;
                StatusText = BuildStatusText(statusDetail);
                OnPropertyChanged(nameof(AllPois));
            });

            if (refreshSearch && !string.IsNullOrWhiteSpace(_currentSearchKeyword))
                await SearchPoisAsync(_currentSearchKeyword, true);
        }
        finally
        {
            _runtimeStateLock.Release();
        }
    }

    private string ResolveRuntimeStatusDetail(PoiRuntimeSnapshot snapshot)
    {
        var tourStatus = BuildTourStatus(snapshot.ActiveTourSession);

        if (!snapshot.IsInitialized)
            return CombineStatusSegments(tourStatus, _text["Home.MapStatusDefault"]);

        if (!snapshot.HasLocationPermission)
            return CombineStatusSegments(
                tourStatus,
                _text.Format("Status.LocationPermissionMissing", snapshot.Pois.Count));

        if (!string.IsNullOrWhiteSpace(snapshot.LastGeofenceReason))
        {
            var nearestPoiText = snapshot.NearestPoi is null
                ? _text["Home.NearestUnknown"]
                : _text.Format("Home.NearestPrefix", snapshot.NearestPoi.Name);

            return CombineStatusSegments(
                tourStatus,
                $"{snapshot.CurrentLocation.Latitude:F5}, {snapshot.CurrentLocation.Longitude:F5} | " +
                $"{nearestPoiText} | {snapshot.LastGeofenceReason}");
        }

        var runtimeStatus = snapshot.IsGpsListening
            ? _text.Format("Status.GpsActive", snapshot.Pois.Count)
            : _text.Format("Status.GpsCannotStart", snapshot.Pois.Count);

        return CombineStatusSegments(tourStatus, runtimeStatus);
    }

    public async Task SearchPoisAsync(string? keyword, bool closeSuggestions = false)
    {
        var normalizedKeyword = PoiService.NormalizeSearchText(keyword);

        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            await ClearPoiSearchAsync();
            return;
        }

        var response = await _poiService.SearchPoisAsync(keyword);
        _currentSearchKeyword = keyword?.Trim() ?? string.Empty;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ReplaceCollection(DisplayedPois, OrderPoisForDisplay(CurrentLocation, response.Results, _activeTourSession));
            ReplaceCollection(SearchSuggestions, response.Suggestions.Select(poi => new HomePoiSuggestion
            {
                Poi = poi
            }));

            ShowSearchSuggestions = !closeSuggestions && SearchSuggestions.Count > 0;
            SearchSuggestionHeight = SearchSuggestions.Count switch
            {
                <= 0 => 0,
                > 5 => 280,
                _ => SearchSuggestions.Count * 56
            };
            ShowSearchEmptyState = DisplayedPois.Count == 0;
            StatusText = BuildStatusText(
                DisplayedPois.Count == 0
                    ? _text["Home.SearchEmptyMessage"]
                    : _text.Format("Status.DisplayingPoisCount", DisplayedPois.Count));
        });
    }

    public async Task ClearPoiSearchAsync()
    {
        var pois = await _poiService.GetAllPoisAsync();
        _currentSearchKeyword = string.Empty;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ApplyPoiCatalog(CurrentLocation, pois, _activeTourSession);
            UpdateNearestPoi(CurrentLocation, pois, _activeTourSession);
            ReplaceCollection(SearchSuggestions, []);
            ShowSearchSuggestions = false;
            SearchSuggestionHeight = 0;
            ShowSearchEmptyState = false;
            StatusText = BuildStatusText(_text.Format("Status.DisplayingPoisCount", DisplayedPois.Count));
        });
    }

    public Poi? FindLoadedPoiById(int poiId)
        => _allPois.FirstOrDefault(x => x.Id == poiId);

    public Poi? FindPoiByRestaurantName(string? restaurantName)
    {
        var restaurantKey = PoiService.NormalizeSearchText(restaurantName);
        if (string.IsNullOrWhiteSpace(restaurantKey))
            return null;

        return _allPois.FirstOrDefault(x =>
                   PoiService.NormalizeSearchText(x.Name) == restaurantKey)
               ?? _allPois.FirstOrDefault(x =>
                   PoiService.NormalizeSearchText(x.Name).Contains(restaurantKey, StringComparison.Ordinal) ||
                   restaurantKey.Contains(PoiService.NormalizeSearchText(x.Name), StringComparison.Ordinal));
    }

    public bool IsPoiNarrationPlaying(int poiId)
        => _narrationUiState.IsPlaying && _narrationUiState.PoiId == poiId;

    public async Task PlayPoiAudioAsync(Poi? poi)
{
    if (poi is null || ShouldIgnoreNarrationAction())
        return;

        if (IsPoiNarrationPlaying(poi.Id))
        {
            await StopNarrationAsync();
            await MainThread.InvokeOnMainThreadAsync(() => NearestPoi = poi);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            NearestPoi = poi;
            _narrationUiState.SetContext(
                poi,
                _settingsService.NarrationOutputMode,
                _settingsService.NarrationLanguage);
            RefreshNarrationState();
            StatusText = BuildStatusText(_text.Format("Status.PoiSelectedSwitching", poi.Name));
        });

        try
        {
            await _narrationService.PlayPoiAsync(poi.Id);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RefreshNarrationState();
                StatusText = BuildStatusText(_text.Format("Status.PlayingPoi", poi.Name));
            });
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText(_text["Status.PlaybackCancelled"]);
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText(_text["Status.PlaybackFailed"]);
            });
        }
    }

    public async Task PreviewNarrationAsync(Poi poi)
    {
        if (poi is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            NearestPoi = poi;
            _narrationUiState.SetContext(
                poi,
                _settingsService.NarrationOutputMode,
                _settingsService.NarrationLanguage);
            RefreshNarrationState();
            StatusText = BuildStatusText(_text.Format("Status.PreviewPlaying", poi.Name));
        });

        await _narrationService.PlayPoiAsync(poi.Id);
    }

    public async Task StopNarrationAsync()
{
    if (ShouldIgnoreNarrationAction())
        return;

    await _narrationService.StopAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RefreshNarrationState();
            StatusText = BuildStatusText(_text["Status.StoppedNarration"]);
        });
    }

    public async Task CloseMiniPlayerAsync()
    {
        await _narrationService.StopAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _narrationUiState.Clear();
            RefreshNarrationState();
            StatusText = BuildStatusText(_text["Status.StoppedNarration"]);
        });
    }

    public void SaveNarrationSettings()
    {
        _settingsService.NarrationLanguage = SelectedLanguage?.Code ?? "vi";
        _settingsService.NarrationOutputMode = SelectedOutputMode;

        StatusText = BuildStatusText(_text["Status.SettingsSaved"]);
        OnPropertyChanged(nameof(NarrationSummary));
    }

    public void RefreshNarrationSettings()
    {
        LoadNarrationSettings();
        RefreshLocalizedText();
    }

    private void LoadNarrationSettings()
    {
        var savedLanguage = _settingsService.NarrationLanguage;
        var savedMode = _settingsService.NarrationOutputMode;

        SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Code == savedLanguage)
                           ?? LanguageOptions.First(x => x.Code == "vi");

        SelectedOutputMode = OutputModeOptions.FirstOrDefault(x => x == savedMode) ?? "Auto";
    }

    private void ApplyPoiCatalog(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        _allPois.Clear();
        _allPois.AddRange(pois);
        UpdateNearbyPois(location, _allPois, activeTourSession);

        if (string.IsNullOrWhiteSpace(_currentSearchKeyword))
        {
            ReplaceCollection(DisplayedPois, _defaultPois);
            ShowSearchEmptyState = DisplayedPois.Count == 0;
        }
    }

    private void UpdateNearbyPois(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        var orderedPois = OrderPoisForDisplay(location, pois, activeTourSession)
            .Take(10)
            .ToList();

        _defaultPois = orderedPois;
        NearbyPois.Clear();

        foreach (var poi in orderedPois)
            NearbyPois.Add(poi);
    }

    private void UpdateNearestPoi(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        NearestPoi = FindPriorityPoi(location, pois, activeTourSession);
    }

    private async Task RefreshPoisFromWebAsync()
    {
        if (!await _syncLock.WaitAsync(0))
            return;

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText(_text["Status.SyncingWeb"]);
            });

            var syncResult = await _poiSyncService.SyncPoisAsync();
            await _poiRuntimeService.ReloadPoisAsync();
            var activePoiCount = _poiRuntimeService.GetSnapshot().Pois.Count;
            _syncSummary = BuildSyncSummary(syncResult, activePoiCount);

            await SyncFromRuntimeStateAsync(
                syncResult.Success
                    ? _text.Format("Status.SyncCompleted", activePoiCount)
                    : _text.Format("Status.SyncFailed", activePoiCount),
                refreshSearch: !string.IsNullOrWhiteSpace(_currentSearchKeyword));
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private string BuildSyncSummary(PoiSyncService.PoiSyncResult syncResult, int activePoiCount)
    {
        if (syncResult.Success)
            return _text.Format("Status.SyncSummarySuccess", syncResult.RemoteCount);

        if (string.IsNullOrWhiteSpace(syncResult.ErrorMessage))
            return _text.Format("Status.SyncSummaryFallback", activePoiCount);

        return _text.Format("Status.SyncSummaryFallbackWithError", activePoiCount, syncResult.ErrorMessage);
    }

    private static List<Poi> OrderPoisForDisplay(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        var orderedPois = pois
            .OrderBy(p => Location.CalculateDistance(
                location.Latitude,
                location.Longitude,
                p.Latitude,
                p.Longitude,
                DistanceUnits.Kilometers))
            .ToList();

        var currentStopPoiId = activeTourSession?.CurrentStop?.PoiId
                               ?? activeTourSession?.CurrentStop?.Poi?.Id;

        if (!currentStopPoiId.HasValue || currentStopPoiId.Value <= 0)
            return orderedPois;

        var currentStopIndex = orderedPois.FindIndex(x => x.Id == currentStopPoiId.Value);
        if (currentStopIndex <= 0)
            return orderedPois;

        var currentStopPoi = orderedPois[currentStopIndex];
        orderedPois.RemoveAt(currentStopIndex);
        orderedPois.Insert(0, currentStopPoi);
        return orderedPois;
    }

    private static Poi? FindPriorityPoi(Location location, IEnumerable<Poi> pois, TourSession? activeTourSession)
    {
        var currentStopPoiId = activeTourSession?.CurrentStop?.PoiId
                               ?? activeTourSession?.CurrentStop?.Poi?.Id;

        if (currentStopPoiId.HasValue && currentStopPoiId.Value > 0)
        {
            var currentStopPoi = pois.FirstOrDefault(x => x.Id == currentStopPoiId.Value);
            if (currentStopPoi is not null)
                return currentStopPoi;
        }

        return pois
            .Select(p => new
            {
                Poi = p,
                Distance = Location.CalculateDistance(
                    location.Latitude,
                    location.Longitude,
                    p.Latitude,
                    p.Longitude,
                    DistanceUnits.Kilometers)
            })
            .OrderBy(x => x.Distance)
            .Select(x => x.Poi)
            .FirstOrDefault();
    }

    private static string CombineStatusSegments(string? primary, string secondary)
    {
        return string.IsNullOrWhiteSpace(primary)
            ? secondary
            : $"{primary} | {secondary}";
    }

    private string? BuildTourStatus(TourSession? session)
    {
        if (session is null)
            return null;

        if (session.IsFinished)
            return _text.Format("Tour.HomeFinished", session.TourName);

        if (session.CurrentStop?.Poi is { } currentPoi)
            return _text.Format("Tour.HomeRouting", session.TourName, currentPoi.Name);

        return _text.Format("Tour.HomeRunning", session.TourName);
    }

    private string BuildStatusText(string detail)
    {
        return $"{_syncSummary} | {detail} | {NarrationSummary}";
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
            target.Add(item);
    }

    public void RefreshLocalizedText()
    {
        _syncSummary = string.IsNullOrWhiteSpace(_syncSummary)
            ? _text["Status.UsingLocalData"]
            : _syncSummary;

        OnPropertyChanged(nameof(NarrationSummary));
        OnPropertyChanged(nameof(NearestPoiText));
        OnPropertyChanged(nameof(SearchEmptyMessage));
        RefreshNarrationState();

        var snapshot = _poiRuntimeService.GetSnapshot();
        StatusText = BuildStatusText(
            string.IsNullOrWhiteSpace(StatusText)
                ? _text["Home.MapStatusDefault"]
                : ResolveRuntimeStatusDetail(snapshot));
    }

    public void RefreshNarrationState()
    {
        ShowMiniPlayer = _narrationUiState.HasContext;
        IsNarrationPlaying = _narrationUiState.IsPlaying;
        CurrentNarrationImageUrl = _narrationUiState.ImageUrl;

        if (!_narrationUiState.HasContext)
        {
            CurrentNarrationTitle = _text["Home.NoNarrationTitle"];
            CurrentNarrationSubtitle = _text["Home.NoNarrationSubtitle"];
            return;
        }

        CurrentNarrationTitle = string.IsNullOrWhiteSpace(_narrationUiState.PoiName)
            ? _text["Home.PausedTitle"]
            : _narrationUiState.PoiName;

        CurrentNarrationSubtitle = _narrationUiState.IsPlaying
            ? $"{_text.GetModeDisplay(_narrationUiState.Mode)} • {_text.GetLanguageDisplay(_narrationUiState.Language)}"
            : _text["Home.PausedSubtitle"];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class LanguageOption
    {
        public LanguageOption(string displayName, string code)
        {
            DisplayName = displayName;
            Code = code;
        }

        public string DisplayName { get; }
        public string Code { get; }

        public override string ToString() => DisplayName;
    }
}
