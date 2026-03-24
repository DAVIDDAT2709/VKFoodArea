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

    private bool _isInitialized;

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

    private string _statusText = "Sẵn sàng phát thuyết minh.";
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
        $"Ngôn ngữ: {SelectedLanguage?.DisplayName ?? "Tiếng Việt"} | Chế độ: {SelectedOutputMode}";

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
            ? "Chưa xác định quán gần nhất"
            : $"Gần nhất: {NearestPoi.Name}";

    public HomeViewModel(
        PoiRepository poiRepository,
        LocationTrackerService locationTrackerService,
        GeofenceEngine geofenceEngine,
        NarrationService narrationService,
        PermissionService permissionService,
        AppSettingsService settingsService,
        PoiSyncService poiSyncService)
    {
        _poiRepository = poiRepository;
        _locationTrackerService = locationTrackerService;
        _geofenceEngine = geofenceEngine;
        _narrationService = narrationService;
        _permissionService = permissionService;
        _settingsService = settingsService;

        _locationTrackerService.LocationChanged += OnLocationChanged;

        PlayPoiAudioCommand = new Command<Poi>(async poi => await PlayPoiAudioAsync(poi));
        _poiSyncService = poiSyncService;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        LoadNarrationSettings();

        var pois = await _poiRepository.GetActiveAsync();
        await _poiSyncService.SyncPoisAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = DefaultLocation;
            UpdateNearbyPois(location: DefaultLocation, pois);
            UpdateNearestPoi(location: DefaultLocation, pois);
            StatusText = $"Đang tải dữ liệu vị trí | {NarrationSummary}";
        });

        var hasPermission = await _permissionService.EnsureLocationPermissionAsync();

        if (!hasPermission)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentLocation = DefaultLocation;
                UpdateNearbyPois(DefaultLocation, pois);
                UpdateNearestPoi(DefaultLocation, pois);
                StatusText = "Chưa cấp quyền vị trí. App đang dùng vị trí mặc định.";
            });

            _isInitialized = true;
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
                ? $"GPS đang hoạt động | {NarrationSummary}"
                : "Không thể bật theo dõi GPS, app đang dùng vị trí hiện tại gần nhất.";
        });

        _isInitialized = true;
    }

    public async Task PlayPoiAudioAsync(Poi? poi)
    {
        if (poi is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            NearestPoi = poi;
            StatusText = $"Đang phát thuyết minh: {poi.Name}";
        });

        await _narrationService.PlayPoiAsync(poi.Id);
    }

    public async Task PreviewNarrationAsync(Poi poi)
    {
        if (poi is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            NearestPoi = poi;
            StatusText = $"Đang phát thử: {poi.Name} | {NarrationSummary}";
        });

        await _narrationService.PlayPoiAsync(poi.Id);
    }

    public async Task StopNarrationAsync()
    {
        await _narrationService.StopAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusText = "Đã dừng phát thuyết minh.";
        });
    }

    public void SaveNarrationSettings()
    {
        _settingsService.NarrationLanguage = SelectedLanguage?.Code ?? "vi";
        _settingsService.NarrationOutputMode = SelectedOutputMode;

        StatusText = $"Đã lưu cài đặt. {NarrationSummary}";
        OnPropertyChanged(nameof(NarrationSummary));
    }

    public void RefreshNarrationSettings()
    {
        LoadNarrationSettings();
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedOutputMode));
        OnPropertyChanged(nameof(NarrationSummary));
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
            StatusText =
                $"Vị trí: {location.Latitude:F5}, {location.Longitude:F5} | " +
                $"{NearestPoiText} | {decision.Reason}";
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
            StatusText = $"Đang phát thuyết minh: {poi.Name} | {NarrationSummary}";
        });

        await _narrationService.PlayPoiAsync(poi.Id);
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