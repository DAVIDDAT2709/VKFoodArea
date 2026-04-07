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
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private bool _isInitialized;
    private string _syncSummary = "Đang dùng dữ liệu local.";

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
        _poiSyncService = poiSyncService;

        _locationTrackerService.LocationChanged += OnLocationChanged;

        PlayPoiAudioCommand = new Command<Poi>(async poi => await PlayPoiAudioAsync(poi));
        StopNarrationCommand = new Command(async () => await StopNarrationAsync());
    }

    public async Task InitializeAsync()
    {
        LoadNarrationSettings();
        var pois = await _poiRepository.GetActiveAsync();

        if (_isInitialized)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateNearbyPois(CurrentLocation, pois);
                UpdateNearestPoi(CurrentLocation, pois);
                StatusText = BuildStatusText($"Đang hiển thị {pois.Count} POI hiện có.");
            });

            _ = RefreshPoisFromWebAsync();
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            CurrentLocation = DefaultLocation;
            UpdateNearbyPois(DefaultLocation, pois);
            UpdateNearestPoi(DefaultLocation, pois);
            StatusText = BuildStatusText($"Đang tải {pois.Count} POI local, tiếp tục đồng bộ web...");
        });

        var hasPermission = await _permissionService.EnsureLocationPermissionAsync();

        if (!hasPermission)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentLocation = DefaultLocation;
                UpdateNearbyPois(DefaultLocation, pois);
                UpdateNearestPoi(DefaultLocation, pois);
                StatusText = BuildStatusText($"Chưa cấp quyền vị trí, đang hiển thị {pois.Count} POI local.");
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
                ? BuildStatusText($"GPS đang hoạt động với {pois.Count} POI hiện tại.")
                : BuildStatusText($"Không thể bật theo dõi GPS. Đang hiển thị {pois.Count} POI hiện tại.");
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
            StatusText = BuildStatusText(detail ?? $"Đang hiển thị {pois.Count} POI.");
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
                StatusText = BuildStatusText(
                    $"Bạn vừa chọn {poi.Name}. Vui lòng chờ 5 giây trước khi phát lại cùng quán.");
            });
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            NearestPoi = poi;
            StatusText = BuildStatusText(
                $"Đã chọn quán: {poi.Name}. Nếu đang phát quán khác, hệ thống sẽ dừng và chuyển sau 3 giây.");
        });

        try
        {
            await _narrationService.PlayPoiAsync(poi);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText($"Đang phát thuyết minh: {poi.Name}");
            });
        }
        catch (OperationCanceledException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText("Đã hủy phát để chuyển sang quán khác.");
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusText = BuildStatusText("Không thể phát thuyết minh. Vui lòng thử lại.");
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
            StatusText = BuildStatusText($"Đang phát thử: {poi.Name}");
        });

        await _narrationService.PlayPoiAsync(poi);
    }

    public async Task StopNarrationAsync()
    {
        await _narrationService.StopAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusText = BuildStatusText("Đã dừng phát thuyết minh.");
        });
    }

    public void SaveNarrationSettings()
    {
        _settingsService.NarrationLanguage = SelectedLanguage?.Code ?? "vi";
        _settingsService.NarrationOutputMode = SelectedOutputMode;

        StatusText = BuildStatusText("Đã lưu cài đặt.");
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
            StatusText = BuildStatusText(
                $"Vị trí: {location.Latitude:F5}, {location.Longitude:F5} | " +
                $"{NearestPoiText} | {decision.Reason}");
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
            StatusText = BuildStatusText($"Đang phát thuyết minh: {poi.Name}");
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
                StatusText = BuildStatusText("Đang đồng bộ dữ liệu web...");
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
                        ? $"Đồng bộ xong, hiện có {pois.Count} POI."
                        : $"Đồng bộ web lỗi, đang dùng {pois.Count} POI local.");
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
            return $"Web: đã đồng bộ {syncResult.RemoteCount} POI";

        if (string.IsNullOrWhiteSpace(syncResult.ErrorMessage))
            return $"Web lỗi, đang dùng {activePoiCount} POI local";

        return $"Web lỗi, đang dùng {activePoiCount} POI local ({syncResult.ErrorMessage})";
    }

    private string BuildStatusText(string detail)
    {
        return $"{_syncSummary} | {detail} | {NarrationSummary}";
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