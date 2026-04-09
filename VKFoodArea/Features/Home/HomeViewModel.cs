using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly PoiRepository _poiRepository;
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

    // Phục vụ hiển thị trạng thái chống spam trên UI
    private DateTime _lastPlayRequestUtc = DateTime.MinValue;
    private int? _lastRequestedPoiId;

    public ObservableCollection<Poi> NearbyPois { get; } = new();

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
        PoiRepository poiRepository,
        LocationTrackerService locationTrackerService,
        GeofenceEngine geofenceEngine,
        NarrationService narrationService,
        PermissionService permissionService,
        AppSettingsService settingsService,
        PoiSyncService poiSyncService,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        _poiRepository = poiRepository;
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
        var pois = await _poiRepository.GetActiveAsync();

        if (_isInitialized)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateNearbyPois(CurrentLocation, pois);
                UpdateNearestPoi(CurrentLocation, pois);
                StatusText = BuildStatusText(_text.Format("Status.DisplayingPoisCount", pois.Count));
            });

            _ = RefreshPoisFromWebAsync();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = DefaultLocation;
            UpdateNearbyPois(DefaultLocation, pois);
            UpdateNearestPoi(DefaultLocation, pois);
            StatusText = BuildStatusText(_text.Format("Status.LoadingLocalAndSyncing", pois.Count));
        });

        var hasPermission = await _permissionService.EnsureLocationPermissionAsync();

        if (!hasPermission)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentLocation = DefaultLocation;
                UpdateNearbyPois(DefaultLocation, pois);
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
            UpdateNearbyPois(location, pois);
            UpdateNearestPoi(location, pois);
        });

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
        var pois = await _poiRepository.GetActiveAsync();

        if (syncResult is not null)
            _syncSummary = BuildSyncSummary(syncResult, pois.Count);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var currentLocation = CurrentLocation;
            UpdateNearbyPois(currentLocation, pois);
            UpdateNearestPoi(currentLocation, pois);
            StatusText = BuildStatusText(detail ?? _text.Format("Status.DisplayingPoisCount", pois.Count));
        });
    }

    public async Task PlayPoiAudioAsync(Poi? poi)
    {
        if (poi is null)
            return;

        var now = DateTime.UtcNow;
        var isSamePoiSpam =
            _lastRequestedPoiId == poi.Id &&
            now - _lastPlayRequestUtc < TimeSpan.FromSeconds(5);

        _lastRequestedPoiId = poi.Id;
        _lastPlayRequestUtc = now;

        if (isSamePoiSpam)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                NearestPoi = poi;
                StatusText = BuildStatusText(_text.Format("Status.SpamSamePoi", poi.Name));
            });
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
            await _narrationService.PlayPoiAsync(poi);

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

        await _narrationService.PlayPoiAsync(poi);
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
        var pois = await _poiRepository.GetActiveAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = location;
            UpdateNearbyPois(location, pois);
            UpdateNearestPoi(location, pois);
        });

        var decision = _geofenceEngine.Evaluate(location.Latitude, location.Longitude, pois);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusText = BuildStatusText(
                $"{location.Latitude:F5}, {location.Longitude:F5} | {NearestPoiText} | {decision.Reason}");
        });

        if (!decision.ShouldTrigger || !decision.PoiId.HasValue)
            return;

        var poi = pois.FirstOrDefault(x => x.Id == decision.PoiId.Value)
                  ?? await _poiRepository.GetByIdAsync(decision.PoiId.Value);

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

        await _narrationService.PlayPoiAsync(poi, "auto");
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
            var pois = await _poiRepository.GetActiveAsync();
            _syncSummary = BuildSyncSummary(syncResult, pois.Count);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var currentLocation = CurrentLocation;
                UpdateNearbyPois(currentLocation, pois);
                UpdateNearestPoi(currentLocation, pois);
                StatusText = BuildStatusText(
                    syncResult.Success
                        ? _text.Format("Status.SyncCompleted", pois.Count)
                        : _text.Format("Status.SyncFailed", pois.Count));
            });
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

    public void RefreshLocalizedText()
    {
        _syncSummary = string.IsNullOrWhiteSpace(_syncSummary)
            ? _text["Status.UsingLocalData"]
            : _syncSummary;

        OnPropertyChanged(nameof(NarrationSummary));
        OnPropertyChanged(nameof(NearestPoiText));
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
