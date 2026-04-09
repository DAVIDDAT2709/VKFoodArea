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
    private readonly PoiService _poiService;
    private readonly LocationTrackerService _locationTrackerService;
    private readonly GeofenceEngine _geofenceEngine;
    private readonly NarrationService _narrationService;
    private readonly PermissionService _permissionService;
    private readonly AppSettingsService _settingsService;
    private readonly PoiSyncService _poiSyncService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

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
    private readonly List<Poi> _allPois = [];
    private List<Poi> _defaultPois = [];
    private string _currentSearchKeyword = string.Empty;

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
        LocationTrackerService locationTrackerService,
        GeofenceEngine geofenceEngine,
        NarrationService narrationService,
        PermissionService permissionService,
        AppSettingsService settingsService,
        PoiSyncService poiSyncService,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        _poiService = poiService;
        _locationTrackerService = locationTrackerService;
        _geofenceEngine = geofenceEngine;
        _narrationService = narrationService;
        _permissionService = permissionService;
        _settingsService = settingsService;
        _poiSyncService = poiSyncService;
        _text = text;
        _narrationUiState = narrationUiState;

        _locationTrackerService.LocationChanged += OnLocationChanged;

        PlayPoiAudioCommand = new Command<Poi>(async poi => await PlayPoiAudioAsync(poi));
        StopNarrationCommand = new Command(async () => await StopNarrationAsync());

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
        var pois = await _poiService.GetAllPoisAsync();

        if (_isInitialized)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ApplyPoiCatalog(CurrentLocation, pois);
                UpdateNearestPoi(CurrentLocation, pois);
                StatusText = BuildStatusText(_text.Format("Status.DisplayingPoisCount", pois.Count));
            });

            if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
                await SearchPoisAsync(_currentSearchKeyword, true);

            _ = RefreshPoisFromWebAsync();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = DefaultLocation;
            ApplyPoiCatalog(DefaultLocation, pois);
            UpdateNearestPoi(DefaultLocation, pois);
            StatusText = BuildStatusText(_text.Format("Status.LoadingLocalAndSyncing", pois.Count));
        });

        var hasPermission = await _permissionService.EnsureLocationPermissionAsync();

        if (!hasPermission)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentLocation = DefaultLocation;
                ApplyPoiCatalog(DefaultLocation, pois);
                UpdateNearestPoi(DefaultLocation, pois);
                StatusText = BuildStatusText(_text.Format("Status.LocationPermissionMissing", pois.Count));
            });

            _isInitialized = true;
            _ = RefreshPoisFromWebAsync();
            return;
        }

        var last = await _locationTrackerService.GetLastKnownAsync();
        var current = await _locationTrackerService.GetCurrentAsync();
        var location = current ?? last ?? DefaultLocation;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = location;
            ApplyPoiCatalog(location, pois);
            UpdateNearestPoi(location, pois);
        });

        if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
            await SearchPoisAsync(_currentSearchKeyword, true);

        var started = await _locationTrackerService.StartListeningAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusText = started
                ? BuildStatusText(_text.Format("Status.GpsActive", pois.Count))
                : BuildStatusText(_text.Format("Status.GpsCannotStart", pois.Count));
        });

        _isInitialized = true;
        _ = RefreshPoisFromWebAsync();
    }

    public async Task RefreshVisiblePoisAsync(
        PoiSyncService.PoiSyncResult? syncResult = null,
        string? detail = null)
    {
        var pois = await _poiService.GetAllPoisAsync();

        if (syncResult is not null)
            _syncSummary = BuildSyncSummary(syncResult, pois.Count);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var currentLocation = CurrentLocation;
            ApplyPoiCatalog(currentLocation, pois);
            UpdateNearestPoi(currentLocation, pois);
            StatusText = BuildStatusText(detail ?? _text.Format("Status.DisplayingPoisCount", pois.Count));
        });

        if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
            await SearchPoisAsync(_currentSearchKeyword, true);
    }

    public async Task<IReadOnlyList<Poi>> GetMapPoisAsync()
    {
        var pois = await _poiService.GetAllPoisAsync();
        return pois.Count == 0 ? NearbyPois.ToList() : pois;
    }

    public async Task<bool> RefreshCurrentLocationAsync()
    {
        var pois = await _poiService.GetAllPoisAsync();
        var effectivePois = pois.Count == 0 ? NearbyPois.ToList() : pois;
        var location = await _locationTrackerService.GetCurrentAsync()
                       ?? await _locationTrackerService.GetLastKnownAsync();

        if (location is null)
            return false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = location;
            ApplyPoiCatalog(location, effectivePois);
            UpdateNearestPoi(location, effectivePois);
            StatusText = BuildStatusText(
                _text.Format("Status.GpsActive", effectivePois.Count));
        });

        if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
            await SearchPoisAsync(_currentSearchKeyword, true);

        return true;
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
            ReplaceCollection(DisplayedPois, response.Results);
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
            ApplyPoiCatalog(CurrentLocation, pois);
            UpdateNearestPoi(CurrentLocation, pois);
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
        if (poi is null)
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
        await _narrationService.StopAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
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

    private async void OnLocationChanged(object? sender, Location location)
    {
        var pois = await _poiService.GetAllPoisAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = location;
            ApplyPoiCatalog(location, pois);
            UpdateNearestPoi(location, pois);
        });

        if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
            await SearchPoisAsync(_currentSearchKeyword, true);

        var decision = _geofenceEngine.Evaluate(location.Latitude, location.Longitude, pois);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusText = BuildStatusText(
                $"{location.Latitude:F5}, {location.Longitude:F5} | {NearestPoiText} | {decision.Reason}");
        });

        if (!decision.ShouldTrigger || !decision.PoiId.HasValue)
            return;

        var poi = pois.FirstOrDefault(x => x.Id == decision.PoiId.Value)
                  ?? await _poiService.GetPoiByIdAsync(decision.PoiId.Value);

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
            StatusText = BuildStatusText(_text.Format("Status.PlayingPoi", poi.Name));
        });

        await _narrationService.PlayPoiAsync(poi.Id, "auto");
    }

    private void ApplyPoiCatalog(Location location, IEnumerable<Poi> pois)
    {
        _allPois.Clear();
        _allPois.AddRange(pois);
        UpdateNearbyPois(location, _allPois);

        if (string.IsNullOrWhiteSpace(_currentSearchKeyword))
        {
            ReplaceCollection(DisplayedPois, _defaultPois);
            ShowSearchEmptyState = DisplayedPois.Count == 0;
        }
    }

    private void UpdateNearbyPois(Location location, IEnumerable<Poi> pois)
    {
        var orderedPois = pois
            .OrderBy(p => Location.CalculateDistance(
                location.Latitude,
                location.Longitude,
                p.Latitude,
                p.Longitude,
                DistanceUnits.Kilometers))
            .Take(10)
            .ToList();

        _defaultPois = orderedPois;
        NearbyPois.Clear();

        foreach (var poi in orderedPois)
            NearbyPois.Add(poi);
    }

    private void UpdateNearestPoi(Location location, IEnumerable<Poi> pois)
    {
        var nearest = pois
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
            .FirstOrDefault();

        NearestPoi = nearest?.Poi;
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
            var pois = await _poiService.GetAllPoisAsync();
            _syncSummary = BuildSyncSummary(syncResult, pois.Count);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var currentLocation = CurrentLocation;
                ApplyPoiCatalog(currentLocation, pois);
                UpdateNearestPoi(currentLocation, pois);
                StatusText = BuildStatusText(
                    syncResult.Success
                        ? _text.Format("Status.SyncCompleted", pois.Count)
                        : _text.Format("Status.SyncFailed", pois.Count));
            });

            if (!string.IsNullOrWhiteSpace(_currentSearchKeyword))
                await SearchPoisAsync(_currentSearchKeyword, true);
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

        if (string.IsNullOrWhiteSpace(StatusText))
            StatusText = BuildStatusText(_text["Home.MapStatusDefault"]);
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
