using Mapsui;
using Mapsui.Extensions;
using Mapsui.Features;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.UI.Maui.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System.ComponentModel;
using System.Globalization;
using VKFoodArea.Features.User;
using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class FullMapPage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly NarrationService _narrationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly LocationTrackerService _locationTrackerService;
    private readonly TourSessionService _tourSessionService;
    private readonly TourNarrationService _tourNarrationService;
    private readonly FoodRepository _foodRepository;
    private readonly AppAuthorizationService _appAuthorizationService;

    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private List<Poi> _allPois = new();
    private TourSession? _activeTourSession;
    private FoodItem? _suggestedFood;
    private Poi? _suggestedFoodPoi;
    private Poi? _focusedMapPoi;
    private bool _isOnlineMapMode;

    private Location? _currentGpsLocation;
    private bool _followMyLocation = true;
    private bool _showDeveloperOptions;
    private int _developerTapCount;
    private DateTimeOffset _developerTapWindowStart = DateTimeOffset.MinValue;

    private const double OcVuLatitude = 10.761403;
    private const double OcVuLongitude = 106.702705;

    private const double OcThaoLatitude = 10.7616799;
    private const double OcThaoLongitude = 106.7023636;

    private const double OcOanhLatitude = 10.7607194;
    private const double OcOanhLongitude = 106.7032972;

    private const double OtXiemLatitude = 10.7611663;
    private const double OtXiemLongitude = 106.7057009;

    private const double OcLoanLatitude = 10.761224;
    private const double OcLoanLongitude = 106.702629;

    private const double DemoMidLatitude = 10.7613135;
    private const double DemoMidLongitude = 106.702667;

    private const string DemoChoiceCurrentTourStop = "Tour: điểm hiện tại";
    private const string DemoChoiceRunRoute = "Chạy lộ trình mẫu";
    private const string DemoChoiceOcVu = "Ốc Vũ";
    private const string DemoChoiceOcThao = "Ốc Thảo";
    private const string DemoChoiceOcOanh = "Ốc Oanh";
    private const string DemoChoiceOtXiem = "Ớt Xiêm";
    private const string DemoChoiceMiddle = "Điểm giữa";
    private const string DemoChoiceOcLoan = "Ốc Loan";

    private const double DefaultMapLatitude = DemoMidLatitude;
    private const double DefaultMapLongitude = DemoMidLongitude;
    private const double MapFollowDistanceMeters = 3500;
    private Button? DemoMenuButtonView => this.FindByName("DemoMenuButton") as Button;
    private Button? RealGpsButtonView => this.FindByName("RealGpsButton") as Button;
    private Label? DemoModeLabelView => this.FindByName("DemoModeLabel") as Label;

    private const string PoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 64 64'>" +
        "<path d='M32 4C20.402 4 11 13.402 11 25c0 14.5 21 35 21 35s21-20.5 21-35C53 13.402 43.598 4 32 4z' fill='#ff1f1f'/>" +
        "<circle cx='32' cy='25' r='10' fill='white'/>" +
        "</svg>";

    private const string NearestPoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='56' height='56' viewBox='0 0 72 72'>" +
        "<circle cx='36' cy='26' r='18' fill='#F7D774' opacity='0.42'/>" +
        "<path d='M36 6C23.85 6 14 15.85 14 28c0 15.25 22 36 22 36s22-20.75 22-36C58 15.85 48.15 6 36 6z' fill='#1F9D74'/>" +
        "<circle cx='36' cy='28' r='10' fill='white'/>" +
        "<circle cx='36' cy='28' r='4' fill='#1F9D74'/>" +
        "</svg>";

    private const string TourPoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='50' height='50' viewBox='0 0 64 64'>" +
        "<path d='M32 5C20.95 5 12 13.95 12 25c0 14 20 34 20 34s20-20 20-34C52 13.95 43.05 5 32 5z' fill='#F59E0B'/>" +
        "<circle cx='32' cy='25' r='10' fill='white'/>" +
        "<path d='M27 25l4 4 7-9' fill='none' stroke='#F59E0B' stroke-width='4' stroke-linecap='round' stroke-linejoin='round'/>" +
        "</svg>";
    private static readonly string[] PoiPinPalette =
    [
        "#2F80ED",
        "#EB5757",
        "#27AE60",
        "#F2994A",
        "#00A3A3",
        "#7B61FF",
        "#D946EF",
        "#14B8A6",
        "#E11D48",
        "#CA8A04",
        "#2563EB"
    ];

    private static readonly IReadOnlyList<double> OfflineMapResolutions = Enumerable
        .Range(0, 22)
        .Select(level => 156543.03392804097 / Math.Pow(2, level))
        .ToArray();

    private static readonly IReadOnlyList<DemoMapPoint> DemoRoutePoints =
    [
        new(DemoChoiceOcVu, "poi:oc-vu", OcVuLatitude, OcVuLongitude),
        new(DemoChoiceOcThao, "poi:oc-thao", OcThaoLatitude, OcThaoLongitude),
        new(DemoChoiceOcOanh, "poi:oc-oanh", OcOanhLatitude, OcOanhLongitude),
        new(DemoChoiceOtXiem, "poi:ot-xiem-quan", OtXiemLatitude, OtXiemLongitude)
    ];

    public FullMapPage(
        HomeViewModel viewModel,
        NarrationService narrationService,
        IServiceProvider serviceProvider,
        AppTextService text,
        NarrationUiStateService narrationUiState,
        LocationTrackerService locationTrackerService,
        TourSessionService tourSessionService,
        TourNarrationService tourNarrationService,
        FoodRepository foodRepository,
        AppAuthorizationService appAuthorizationService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _narrationService = narrationService;
        _serviceProvider = serviceProvider;
        _text = text;
        _narrationUiState = narrationUiState;
        _locationTrackerService = locationTrackerService;
        _tourSessionService = tourSessionService;
        _tourNarrationService = tourNarrationService;
        _foodRepository = foodRepository;
        _appAuthorizationService = appAuthorizationService;

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _narrationUiState.StateChanged += OnNarrationUiStateChanged;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _tourSessionService.StateChanged -= OnTourSessionStateChanged;
        _tourSessionService.StateChanged += OnTourSessionStateChanged;

        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        ApplyStaticText();
        SetDemoUi(false);

        await _viewModel.InitializeAsync();
        _allPois = (await _viewModel.GetMapPoisAsync()).ToList();

        RefreshActiveTourContext();
        await RefreshFoodSuggestionAsync();
        RefreshMapLocationFromViewModel(true);
        _ = TryPlayTourIntroAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _tourSessionService.StateChanged -= OnTourSessionStateChanged;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void ApplyStaticText()
    {
        Title = "Bản đồ khám phá";
        PageTitleLabel.Text = "Bản đồ khám phá";
        MapSubtitleLabel.Text = "Khu Vĩnh Khánh, Quận 4";
        MapBadgeLabel.Text = "Bản đồ";
        ApplyMapModeText();
        TourContextPanel.IsVisible = false;
        FoodSuggestionPanel.IsVisible = false;

        if (DemoMenuButtonView is not null)
        {
            DemoMenuButtonView.Text = "Mẫu";
            DemoMenuButtonView.IsVisible = _appAuthorizationService.CanUseInternalTools && _showDeveloperOptions;
        }

        if (RealGpsButtonView is not null)
            RealGpsButtonView.Text = "GPS";

        NavHomeButton.Text = "Trang chủ";
        NavMapButton.Text = "Bản đồ";
        NavHistoryButton.Text = "Lịch sử";
        NavAccountButton.Text = "Tài khoản";
        MiniPlayerStopButton.Text = "Dừng";
        OpenExternalMapButton.Text = "Mở Maps";
    }
    private void OnDeveloperHeaderTapped(object sender, TappedEventArgs e)
    {
        if (!_appAuthorizationService.CanUseInternalTools)
            return;

        var now = DateTimeOffset.UtcNow;

        if (now - _developerTapWindowStart > TimeSpan.FromSeconds(6))
        {
            _developerTapWindowStart = now;
            _developerTapCount = 0;
        }

        _developerTapCount++;

        if (_developerTapCount < 5)
            return;

        _showDeveloperOptions = !_showDeveloperOptions;
        _developerTapCount = 0;
        _developerTapWindowStart = DateTimeOffset.MinValue;

        if (DemoMenuButtonView is not null)
            DemoMenuButtonView.IsVisible = _showDeveloperOptions;

        InfoLabel.Text = _showDeveloperOptions
            ? "Đã mở vị trí mẫu."
            : "Đã ẩn vị trí mẫu.";
    }

    private void SetDemoUi(bool isDemoMode, string? label = null)
    {
        if (RealGpsButtonView is not null)
            RealGpsButtonView.IsVisible = isDemoMode;

        if (DemoModeLabelView is not null)
        {
            DemoModeLabelView.IsVisible = isDemoMode;
            DemoModeLabelView.Text = isDemoMode
                ? $"Vị trí mẫu: {label}"
                : string.Empty;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.AllPois))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                _allPois = (await _viewModel.GetMapPoisAsync()).ToList();
                RefreshActiveTourContext();
                RefreshMapSurface(_followMyLocation);
                UpdateNearestPoiInfo();
                await RefreshFoodSuggestionAsync();
            });
            return;
        }

        if (e.PropertyName == nameof(HomeViewModel.CurrentLocation))
        {
            MainThread.BeginInvokeOnMainThread(() =>
                RefreshMapLocationFromViewModel(_followMyLocation));
        }
    }

    private void RefreshMapLocationFromViewModel(bool shouldCenterMap)
    {
        _currentGpsLocation = _viewModel.CurrentLocation;
        RefreshActiveTourContext();
        UpdateNearestPoiInfo();
        RefreshMapSurface(shouldCenterMap);
        _ = RefreshFoodSuggestionAsync();
    }

    private void InitializeMap()
    {
        if (_mapControl is null)
        {
            _mapControl = new MapControl
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            _mapControl.MapTapped += OnMapTapped;
            MapHost.Content = _mapControl;
        }

        _mapControl.Map = BuildMap();
        _mapControl.Refresh();
    }

    private void RefreshMapSurface(bool shouldCenterMap)
    {
        if (_mapControl is null)
        {
            InitializeMap();
            return;
        }

        _mapControl.Map = BuildMap();

        if (shouldCenterMap && _mapControl.Map is not null)
            CenterMap(_mapControl.Map);

        _mapControl.Refresh();
    }

    private Mapsui.Map BuildMap()
    {
        _isOnlineMapMode = ShouldUseOnlineMap();
        ApplyMapModeText();

        var map = new Mapsui.Map
        {
            BackColor = Mapsui.Styles.Color.FromString("#F1F6F2")
        };
        map.Widgets.Clear();

        var nearestPoiId = GetNearestPoiIdForCurrentLocation();
        var isTourMap = HasActiveTour();
        var visiblePois = GetDisplayMapPois(GetVisibleMapPois(), isTourMap);

        if (_isOnlineMapMode)
        {
            map.Layers.Add(OpenStreetMap.CreateTileLayer());
        }
        else
        {
            map.Navigator.OverrideResolutions = OfflineMapResolutions;

            foreach (var layer in OfflineMapLayerFactory.CreateVinhKhanhLayers())
                map.Layers.Add(layer);
        }

        _poiLayer = new MemoryLayer("POIs")
        {
            Features = visiblePois
                .Select((poi, index) => CreatePoiFeature(poi, nearestPoiId == poi.Id, isTourMap, index + 1))
                .Cast<IFeature>()
                .ToList()
        };

        var currentLocationFeature = CreateCurrentLocationFeature();

        var currentLocationLayer = new MemoryLayer("CurrentLocation")
        {
            Features = currentLocationFeature is null
                ? []
                : [currentLocationFeature]
        };

        map.Layers.Add(_poiLayer);
        if (!_isOnlineMapMode)
            map.Layers.Add(CreateOfflinePoiLabelLayer(visiblePois, isTourMap));

        map.Layers.Add(currentLocationLayer);

        CenterMap(map);
        return map;
    }

    private List<Poi> GetDisplayMapPois(IReadOnlyList<Poi> sourcePois, bool isTourMap)
    {
        if (_isOnlineMapMode || isTourMap)
            return sourcePois.ToList();

        return sourcePois
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Take(10)
            .ToList();
    }

    private void CenterMap(Mapsui.Map map)
    {
        var location = GetMapCenterLocation();
        var center = SphericalMercator
            .FromLonLat(location.Longitude, location.Latitude)
            .ToMPoint();

        var zoomResolution = GetOfflineMapResolution(map, 19);

        map.Navigator.CenterOnAndZoomTo(center, zoomResolution);
    }

    private static double GetOfflineMapResolution(Mapsui.Map map, int preferredLevel)
    {
        var resolutions = map.Navigator.Resolutions;
        if (resolutions.Count > 0)
            return resolutions[Math.Min(preferredLevel, resolutions.Count - 1)];

        return OfflineMapResolutions[Math.Min(preferredLevel, OfflineMapResolutions.Count - 1)];
    }

    private Location GetMapCenterLocation()
    {
        var currentStopPoi = _activeTourSession?.CurrentStop?.Poi;
        if (currentStopPoi is not null)
            return new Location(currentStopPoi.Latitude, currentStopPoi.Longitude);

        var visiblePois = GetVisibleMapPois();
        if (OfflineMapLayerFactory.IsNearAnyPoi(_currentGpsLocation, visiblePois, MapFollowDistanceMeters))
            return _currentGpsLocation!;

        if (visiblePois.Count > 0)
            return OfflineMapLayerFactory.GetContentCenter(visiblePois);

        return new Location(DefaultMapLatitude, DefaultMapLongitude);
    }

    private int? GetNearestPoiIdForCurrentLocation()
    {
        if (_activeTourSession?.CurrentStop is { PoiId: > 0 } currentStop)
            return currentStop.PoiId;

        var visiblePois = GetVisibleMapPois();

        if (_currentGpsLocation is null || visiblePois.Count == 0)
            return null;

        Poi? nearestPoi = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var poi in visiblePois)
        {
            var distanceMeters = Location.CalculateDistance(
                _currentGpsLocation.Latitude,
                _currentGpsLocation.Longitude,
                poi.Latitude,
                poi.Longitude,
                DistanceUnits.Kilometers) * 1000;

            if (distanceMeters < nearestDistanceMeters)
            {
                nearestDistanceMeters = distanceMeters;
                nearestPoi = poi;
            }
        }

        return nearestPoi?.Id;
    }

    private void UpdateNearestPoiInfo()
    {
        if (HasActiveTour())
        {
            UpdateTourInfoText();
            return;
        }

        var visiblePois = GetVisibleMapPois();

        if (_currentGpsLocation is null)
        {
            var fallbackPoi = visiblePois
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Name)
                .FirstOrDefault();

            SetMapActionTarget(fallbackPoi);
            InfoLabel.Text = fallbackPoi is null
                ? "Chưa có vị trí hiện tại."
                : $"Đang xem khu Vĩnh Khánh. Bật GPS để thấy quán gần nhất.";
            MapSubtitleLabel.Text = "Khu Vĩnh Khánh, Quận 4";
            return;
        }

        if (visiblePois.Count == 0)
        {
            SetMapActionTarget(null);
            InfoLabel.Text = $"GPS: {_currentGpsLocation.Latitude:F6}, {_currentGpsLocation.Longitude:F6}";
            MapSubtitleLabel.Text = "Chưa có POI để hiển thị";
            return;
        }

        Poi? nearestPoi = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var poi in visiblePois)
        {
            var distanceMeters = Location.CalculateDistance(
                _currentGpsLocation.Latitude,
                _currentGpsLocation.Longitude,
                poi.Latitude,
                poi.Longitude,
                DistanceUnits.Kilometers) * 1000;

            if (distanceMeters < nearestDistanceMeters)
            {
                nearestDistanceMeters = distanceMeters;
                nearestPoi = poi;
            }
        }

        if (nearestPoi is null)
        {
            SetMapActionTarget(null);
            InfoLabel.Text = $"GPS: {_currentGpsLocation.Latitude:F6}, {_currentGpsLocation.Longitude:F6}";
            MapSubtitleLabel.Text = "Khu Vĩnh Khánh, Quận 4";
            return;
        }

        SetMapActionTarget(nearestPoi, nearestDistanceMeters);

        if (nearestDistanceMeters > MapFollowDistanceMeters)
        {
            InfoLabel.Text = $"GPS đang ngoài khu Vĩnh Khánh. Mở Maps để đi tới {nearestPoi.Name}.";
            MapSubtitleLabel.Text = "Khu Vĩnh Khánh, Quận 4";
            return;
        }

        InfoLabel.Text = $"Gần nhất: {nearestPoi.Name} • {nearestDistanceMeters:F0} m";
        MapSubtitleLabel.Text = $"GPS đang hoạt động • {nearestPoi.Name}";
    }

    private static PointFeature CreatePoiFeature(Poi poi, bool isNearest, bool isTourMap, int displayNumber)
    {
        var point = SphericalMercator
            .FromLonLat(poi.Longitude, poi.Latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature["Name"] = poi.Name;
        feature["Address"] = poi.Address;
        feature["PoiId"] = poi.Id;
        feature["DisplayNumber"] = displayNumber;

        feature.Styles.Add(new ImageStyle
        {
            Image = GetPoiPinSvg(poi, isNearest, isTourMap, displayNumber),
            SymbolScale = isNearest ? 0.74 : 0.62,
            Offset = new Offset(0, 20),
            RotateWithMap = false
        });

        return feature;
    }

    private static MemoryLayer CreateOfflinePoiLabelLayer(IReadOnlyList<Poi> pois, bool isTourMap)
    {
        var labelPois = (isTourMap ? pois : pois.Take(6))
            .ToList();

        return new MemoryLayer("Offline POI labels")
        {
            Features = labelPois
                .Select((poi, index) => CreateOfflinePoiLabelFeature(poi, index + 1))
                .Cast<IFeature>()
                .ToList()
        };
    }

    private static PointFeature CreateOfflinePoiLabelFeature(Poi poi, int displayNumber)
    {
        var longitudeOffset = displayNumber % 2 == 0 ? -0.00012 : 0.00012;
        var latitudeOffset = 0.00010 + (displayNumber % 3) * 0.00002;
        var point = SphericalMercator
            .FromLonLat(poi.Longitude + longitudeOffset, poi.Latitude + latitudeOffset)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature.Styles.Add(new LabelStyle
        {
            Text = $"{displayNumber}. {poi.Name}",
            ForeColor = Mapsui.Styles.Color.FromString("#173330"),
            BackColor = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#FFFFFF")),
            Halo = new Pen(Mapsui.Styles.Color.White, 2),
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            CollisionDetection = true
        });

        return feature;
    }

    private static string GetPoiPinSvg(Poi poi, bool isNearest, bool isTourMap, int displayNumber)
    {
        var label = Math.Clamp(displayNumber, 1, 99).ToString();

        if (isNearest)
        {
            return
                "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='64' height='70' viewBox='0 0 64 70'>" +
                "<ellipse cx='32' cy='62' rx='15' ry='5' fill='#173330' opacity='0.22'/>" +
                "<circle cx='32' cy='27' r='24' fill='#F7D774' opacity='0.38'/>" +
                "<path d='M32 5C19.85 5 10 14.85 10 27c0 16.25 22 37 22 37s22-20.75 22-37C54 14.85 44.15 5 32 5z' fill='#1F9D74'/>" +
                "<circle cx='32' cy='27' r='13' fill='white'/>" +
                "<text x='32' y='32' text-anchor='middle' font-size='15' font-family='Arial' font-weight='800' fill='#1F9D74'>" + label + "</text>" +
                "</svg>";
        }

        if (isTourMap)
        {
            return
                "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='60' height='66' viewBox='0 0 64 70'>" +
                "<ellipse cx='32' cy='62' rx='14' ry='5' fill='#173330' opacity='0.18'/>" +
                "<path d='M32 6C20.95 6 12 14.95 12 26c0 15 20 36 20 36s20-21 20-36C52 14.95 43.05 6 32 6z' fill='#F59E0B'/>" +
                "<circle cx='32' cy='26' r='12' fill='white'/>" +
                "<text x='32' y='31' text-anchor='middle' font-size='15' font-family='Arial' font-weight='800' fill='#B45309'>" + label + "</text>" +
                "</svg>";
        }

        var fillColor = PoiPinPalette[Math.Abs(poi.Id.GetHashCode()) % PoiPinPalette.Length];

        return
            "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='58' height='64' viewBox='0 0 64 70'>" +
            "<ellipse cx='32' cy='62' rx='13' ry='5' fill='#173330' opacity='0.15'/>" +
            "<circle cx='32' cy='26' r='22' fill='" + fillColor + "' opacity='0.18'/>" +
            "<path d='M32 6C20.95 6 12 14.95 12 26c0 15 20 36 20 36s20-21 20-36C52 14.95 43.05 6 32 6z' fill='" + fillColor + "'/>" +
            "<circle cx='32' cy='26' r='12' fill='white' opacity='0.96'/>" +
            "<text x='32' y='31' text-anchor='middle' font-size='15' font-family='Arial' font-weight='800' fill='" + fillColor + "'>" + label + "</text>" +
            "</svg>";
    }

    private PointFeature? CreateCurrentLocationFeature()
    {
        if (_currentGpsLocation is null)
            return null;

        if (!_isOnlineMapMode &&
            !OfflineMapLayerFactory.IsNearAnyPoi(_currentGpsLocation, GetVisibleMapPois(), MapFollowDistanceMeters))
        {
            return null;
        }

        var point = SphericalMercator
            .FromLonLat(_currentGpsLocation.Longitude, _currentGpsLocation.Latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature["Name"] = "Vị trí hiện tại";

        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.20,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString("#1D4ED8")),
            Outline = new Pen(Mapsui.Styles.Color.White, 2)
        });

        return feature;
    }

    private void OnZoomInClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        _followMyLocation = false;
        _mapControl.Map.Navigator.ZoomIn();
        _mapControl.Refresh();
    }

    private void OnZoomOutClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        _followMyLocation = false;
        _mapControl.Map.Navigator.ZoomOut();
        _mapControl.Refresh();
    }

    private async void OnMyLocationClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        _locationTrackerService.DisableDemoMode();
        _followMyLocation = true;
        SetDemoUi(false);

        if (!await _viewModel.RefreshCurrentLocationAsync())
        {
            if (_currentGpsLocation is null)
                InfoLabel.Text = "Không lấy được vị trí hiện tại.";
            return;
        }

        RefreshMapLocationFromViewModel(true);
    }

    private async void OnMapTapped(object? sender, MapEventArgs e)
    {
        _followMyLocation = false;

        if (_mapControl?.Map is null || _poiLayer is null)
            return;

        var mapInfo = e.GetMapInfo(new[] { _poiLayer });
        if (mapInfo?.Feature is null)
        {
            UpdateNearestPoiInfo();
            return;
        }

        if (!int.TryParse(mapInfo.Feature["PoiId"]?.ToString(), out var poiId))
            return;

        var poi = GetVisibleMapPois().FirstOrDefault(x => x.Id == poiId)
                  ?? _allPois.FirstOrDefault(x => x.Id == poiId);
        if (poi is null)
            return;

        var distanceMeters = CalculateDistanceMeters(poi);
        SetMapActionTarget(poi, distanceMeters);
        InfoLabel.Text = distanceMeters.HasValue
            ? $"Đã chọn: {poi.Name} • {distanceMeters.Value:F0} m"
            : $"Đã chọn: {poi.Name}";
        await OpenPoiDetailAsync(poi);
    }

    private async void OnOpenExternalMapClicked(object sender, EventArgs e)
    {
        var poi = _focusedMapPoi
                  ?? _activeTourSession?.CurrentStop?.Poi
                  ?? GetVisibleMapPois()
                      .OrderByDescending(x => x.Priority)
                      .ThenBy(x => x.Name)
                      .FirstOrDefault();

        if (poi is null)
        {
            await DisplayAlertAsync("Bản đồ", "Chưa có điểm để mở bản đồ.", _text["Common.Ok"]);
            return;
        }

        try
        {
            await Launcher.OpenAsync(BuildExternalMapUri(poi));
        }
        catch
        {
            await DisplayAlertAsync("Bản đồ", "Không mở được Google Maps lúc này.", _text["Common.Ok"]);
        }
    }

    private static Uri BuildExternalMapUri(Poi poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.MapUrl) &&
            Uri.TryCreate(poi.MapUrl, UriKind.Absolute, out var existingUri))
        {
            return existingUri;
        }

        var latitude = poi.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = poi.Longitude.ToString(CultureInfo.InvariantCulture);
        var query = Uri.EscapeDataString($"{latitude},{longitude}");
        return new Uri($"https://www.google.com/maps/search/?api=1&query={query}");
    }

    private void SetMapActionTarget(Poi? poi, double? distanceMeters = null)
    {
        _focusedMapPoi = poi;
        OpenExternalMapButton.IsVisible = poi is not null;
        OpenExternalMapButton.Text = "Mở Maps";
    }

    private double? CalculateDistanceMeters(Poi poi)
    {
        if (_currentGpsLocation is null)
            return null;

        return Location.CalculateDistance(
            _currentGpsLocation.Latitude,
            _currentGpsLocation.Longitude,
            poi.Latitude,
            poi.Longitude,
            DistanceUnits.Kilometers) * 1000;
    }

    private async Task OpenPoiDetailAsync(Poi poi)
    {
        await Navigation.PushAsync(new PoiDetailPage(
            poi,
            _narrationService,
            _text,
            _narrationUiState,
            _activeTourSession?.TourId,
            _activeTourSession?.TourName));
    }

    private List<Poi> GetVisibleMapPois()
    {
        var session = _activeTourSession;
        if (session?.OrderedStops.Count > 0 && !session.IsFinished)
        {
            var tourPois = new List<Poi>();

            foreach (var stop in session.OrderedStops)
            {
                var poi = _allPois.FirstOrDefault(x => x.Id == stop.PoiId) ?? stop.Poi;
                if (poi is null)
                    continue;

                if (tourPois.All(x => x.Id != poi.Id))
                    tourPois.Add(poi);
            }

            if (tourPois.Count > 0)
                return tourPois;
        }

        return _allPois;
    }

    private bool HasActiveTour()
        => _activeTourSession is { IsFinished: false };

    private void RefreshActiveTourContext()
    {
        _activeTourSession = _tourSessionService.GetCurrentSession();
        var hasTour = HasActiveTour();

        TourContextPanel.IsVisible = hasTour;
        MapBadgeLabel.Text = hasTour ? "Tour" : "Bản đồ";
        PageTitleLabel.Text = hasTour ? "Bản đồ Tour" : "Bản đồ khám phá";
        MapSubtitleLabel.Text = hasTour ? "Đang theo tour" : "Khu Vĩnh Khánh, Quận 4";

        if (!hasTour || _activeTourSession is null)
        {
            TourContextTitleLabel.Text = string.Empty;
            TourContextSubtitleLabel.Text = string.Empty;
            return;
        }

        var session = _activeTourSession;
        var currentStop = session.CurrentStop;
        var completedCount = session.CompletedStopIds.Count;
        var totalCount = session.OrderedStops.Count;

        TourContextTitleLabel.Text = session.TourName;
        TourContextSubtitleLabel.Text = currentStop?.Poi is { } poi
            ? $"Điểm tiếp theo: {poi.Name} • {completedCount}/{totalCount} điểm đã nghe"
            : $"{completedCount}/{totalCount} điểm đã nghe";
    }

    private void UpdateTourInfoText()
    {
        var session = _activeTourSession;
        var currentStopPoi = session?.CurrentStop?.Poi;

        if (session is null || currentStopPoi is null)
        {
            SetMapActionTarget(null);
            InfoLabel.Text = "Tour đang sẵn sàng.";
            return;
        }

        var distanceMeters = CalculateDistanceMeters(currentStopPoi);
        SetMapActionTarget(currentStopPoi, distanceMeters);

        if (_currentGpsLocation is null)
        {
            InfoLabel.Text = $"Tour: {currentStopPoi.Name}. Bật GPS để app tự phát khi đến gần.";
            MapSubtitleLabel.Text = "Đang theo tour";
            return;
        }

        if (!distanceMeters.HasValue)
            return;

        if (distanceMeters.Value > MapFollowDistanceMeters)
        {
            InfoLabel.Text = $"Tour: {currentStopPoi.Name} • GPS hiện tại ngoài khu tour";
            MapSubtitleLabel.Text = "Đang theo tour tại Vĩnh Khánh";
            return;
        }

        InfoLabel.Text = $"Tour: {currentStopPoi.Name} • còn khoảng {distanceMeters.Value:F0} m";
        MapSubtitleLabel.Text = "Đang theo tour";
    }

    private async Task RefreshFoodSuggestionAsync()
    {
        var session = _activeTourSession;
        var poi = session?.CurrentStop?.Poi;

        if (poi is null)
        {
            _suggestedFood = null;
            _suggestedFoodPoi = null;
            FoodSuggestionPanel.IsVisible = false;
            return;
        }

        var foods = await _foodRepository.GetByRestaurantAsync(poi.Name);
        var food = foods.FirstOrDefault();

        if (food is null)
        {
            _suggestedFood = null;
            _suggestedFoodPoi = null;
            FoodSuggestionPanel.IsVisible = false;
            return;
        }

        _suggestedFood = food;
        _suggestedFoodPoi = poi;
        FoodSuggestionTitleLabel.Text = food.Name;
        FoodSuggestionSubtitleLabel.Text = $"{food.RestaurantName} • từ {food.Price:N0}đ";
        FoodSuggestionImage.Source = food.ImageUrl;
        FoodSuggestionPanel.IsVisible = true;
    }

    private async Task TryPlayTourIntroAsync()
    {
        var session = _tourSessionService.GetCurrentSession();
        var currentLanguage = _tourNarrationService.CurrentLanguage;

        if (session is null ||
            session.IsFinished ||
            (session.IntroPlayedAt.HasValue &&
             string.Equals(
                 session.IntroPlayedLanguage,
                 currentLanguage,
                 StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _tourSessionService.MarkIntroPlayed(currentLanguage);

        try
        {
            await _tourNarrationService.PlayIntroAsync(session);
        }
        catch
        {
        }
    }

    private async void OnDemoMenuClicked(object sender, EventArgs e)
    {
        if (!_appAuthorizationService.CanUseInternalTools || !_showDeveloperOptions)
            return;

        var choice = await DisplayActionSheetAsync(
            "Chọn vị trí mẫu",
            "Hủy",
            null,
            BuildDemoChoices());

        if (string.IsNullOrWhiteSpace(choice) || choice == "Hủy")
            return;

        switch (choice)
        {
            case DemoChoiceCurrentTourStop:
                await ActivateCurrentTourStopDemoAsync();
                break;

            case DemoChoiceRunRoute:
                await RunDemoRouteAsync();
                break;

            case DemoChoiceOcVu:
                await ActivateDemoPointAsync(DemoChoiceOcVu, OcVuLatitude, OcVuLongitude);
                break;

            case DemoChoiceOcThao:
                await ActivateDemoPointAsync(DemoChoiceOcThao, OcThaoLatitude, OcThaoLongitude);
                break;

            case DemoChoiceOcOanh:
                await ActivateDemoPointAsync(DemoChoiceOcOanh, OcOanhLatitude, OcOanhLongitude);
                break;

            case DemoChoiceOtXiem:
                await ActivateDemoPointAsync(DemoChoiceOtXiem, OtXiemLatitude, OtXiemLongitude);
                break;

            case DemoChoiceMiddle:
                await ActivateDemoPointAsync(DemoChoiceMiddle, DemoMidLatitude, DemoMidLongitude);
                break;

            case DemoChoiceOcLoan:
                await ActivateDemoPointAsync(DemoChoiceOcLoan, OcLoanLatitude, OcLoanLongitude);
                break;
        }
    }

    private string[] BuildDemoChoices()
    {
        var choices = new List<string>();
        RefreshActiveTourContext();

        if (HasActiveTour())
            choices.Add(DemoChoiceCurrentTourStop);

        choices.Add(DemoChoiceRunRoute);
        choices.AddRange([
            DemoChoiceOcVu,
            DemoChoiceOcThao,
            DemoChoiceOcOanh,
            DemoChoiceOtXiem,
            DemoChoiceMiddle,
            DemoChoiceOcLoan
        ]);

        return choices.ToArray();
    }

    private async Task ActivateCurrentTourStopDemoAsync()
    {
        RefreshActiveTourContext();

        var currentStopPoi = _activeTourSession?.CurrentStop?.Poi;
        if (currentStopPoi is null)
        {
            InfoLabel.Text = "Tour chưa có điểm đang chờ.";
            return;
        }

        await ActivateDemoPointAsync(
            $"Tour - {currentStopPoi.Name}",
            currentStopPoi.Latitude,
            currentStopPoi.Longitude);
    }

    private async Task RunDemoRouteAsync()
    {
        var route = BuildDemoRoute();
        if (route.Count == 0)
        {
            InfoLabel.Text = "Chưa có dữ liệu vị trí mẫu.";
            return;
        }

        for (var index = 0; index < route.Count; index++)
        {
            var point = route[index];
            InfoLabel.Text = $"Demo GPS {index + 1}/{route.Count}: {point.Label}";
            await ActivateDemoPointAsync(point.Label, point.Latitude, point.Longitude);

            if (index < route.Count - 1)
                await Task.Delay(2600);
        }

        InfoLabel.Text = "Đã chạy xong lộ trình mẫu. Mở web để kiểm tra GPS, history và dashboard.";
    }

    private List<DemoMapPoint> BuildDemoRoute()
    {
        RefreshActiveTourContext();

        if (_activeTourSession is { IsFinished: false, OrderedStops.Count: > 0 } session)
        {
            var tourRoute = session.OrderedStops
                .Select(stop => ResolveDemoPoint(stop.Poi, stop.PoiId))
                .Where(point => point is not null)
                .Select(point => point!)
                .ToList();

            if (tourRoute.Count > 0)
                return tourRoute;
        }

        return DemoRoutePoints.ToList();
    }

    private DemoMapPoint? ResolveDemoPoint(Poi? poi, int? poiId = null)
    {
        var matchedPoi = PoiReferenceMatcher.FindMatch(_allPois, poi, poiId) ?? poi;
        if (matchedPoi is null)
            return null;

        var normalizedQrCode = PoiReferenceMatcher.NormalizeQrCode(matchedPoi.QrCode);
        var predefinedPoint = DemoRoutePoints.FirstOrDefault(point =>
            PoiReferenceMatcher.NormalizeQrCode(point.QrCode) == normalizedQrCode);

        return predefinedPoint ?? new DemoMapPoint(
            matchedPoi.Name,
            matchedPoi.QrCode,
            matchedPoi.Latitude,
            matchedPoi.Longitude);
    }

    private async Task ActivateDemoPointAsync(string label, double latitude, double longitude)
    {
        _followMyLocation = true;
        _currentGpsLocation = new Location(latitude, longitude);

        SetDemoUi(true, label);
        UpdateNearestPoiInfo();
        RefreshMapSurface(true);

        _locationTrackerService.SimulateLocation(latitude, longitude);
        await Task.Delay(1200);
        _locationTrackerService.SimulateLocation(latitude, longitude);
        await Task.Delay(300);

        RefreshMapLocationFromViewModel(true);
        await RefreshFoodSuggestionAsync();
    }

    private async void OnRealGpsClicked(object sender, EventArgs e)
    {
        _locationTrackerService.DisableDemoMode();
        _followMyLocation = true;
        SetDemoUi(false);

        var refreshed = await _viewModel.RefreshCurrentLocationAsync();
        if (refreshed)
            RefreshMapLocationFromViewModel(true);
        else
            InfoLabel.Text = "Đã quay về vị trí hiện tại.";
    }

    private async void OnEndTourClicked(object sender, EventArgs e)
    {
        _tourSessionService.Cancel();
        RefreshActiveTourContext();
        await RefreshFoodSuggestionAsync();
        RefreshMapSurface(true);
        UpdateNearestPoiInfo();
    }

    private async void OnFoodSuggestionClicked(object sender, EventArgs e)
    {
        if (_suggestedFoodPoi is null || _suggestedFood is null)
            return;

        InfoLabel.Text = $"Gợi ý: {_suggestedFood.Name}";
        await OpenPoiDetailAsync(_suggestedFoodPoi);
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnMapCurrentClicked(object sender, EventArgs e)
    {
        await Task.CompletedTask;
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<HistoryPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnUserClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<UserPage>();
        await Navigation.PushAsync(page);
    }

    private void OnNarrationUiStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(_viewModel.RefreshNarrationState);
    }

    private static bool ShouldUseOnlineMap()
        => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    private void ApplyMapModeText()
    {
        if (MapModeLabel is null || MapModeDot is null)
            return;

        MapModeLabel.Text = _isOnlineMapMode
            ? "Bản đồ online"
            : "Offline - Vĩnh Khánh";

        MapModeDetailLabel.Text = _isOnlineMapMode
            ? "Có nền bản đồ trực tuyến"
            : "Sơ đồ khu, mở Maps để dẫn đường";

        MapModeDot.Fill = _isOnlineMapMode
            ? Microsoft.Maui.Graphics.Color.FromArgb("#1F9D74")
            : Microsoft.Maui.Graphics.Color.FromArgb("#D59C29");
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var shouldUseOnlineMap = ShouldUseOnlineMap();
            if (shouldUseOnlineMap == _isOnlineMapMode)
                return;

            RefreshMapSurface(true);
        });
    }

    private void OnTourSessionStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            RefreshActiveTourContext();
            await RefreshFoodSuggestionAsync();
            RefreshMapSurface(true);
            UpdateNearestPoiInfo();
        });
    }

    private sealed record DemoMapPoint(string Label, string QrCode, double Latitude, double Longitude);
}
