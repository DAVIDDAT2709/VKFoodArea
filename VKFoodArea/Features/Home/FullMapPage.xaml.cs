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
using Microsoft.Maui.Devices.Sensors;
using System.ComponentModel;
using VKFoodArea.Features.User;
using VKFoodArea.Models;
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

    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private List<Poi> _allPois = new();

    private Location? _currentGpsLocation;
    private bool _followMyLocation = true;

    private const double OcVuLatitude = 10.761403;
    private const double OcVuLongitude = 106.702705;

    private const double OcLoanLatitude = 10.761224;
    private const double OcLoanLongitude = 106.702629;

    private const double DemoMidLatitude = 10.7613135;
    private const double DemoMidLongitude = 106.702667;

    private const double DefaultMapLatitude = DemoMidLatitude;
    private const double DefaultMapLongitude = DemoMidLongitude;
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

    public FullMapPage(
        HomeViewModel viewModel,
        NarrationService narrationService,
        IServiceProvider serviceProvider,
        AppTextService text,
        NarrationUiStateService narrationUiState,
        LocationTrackerService locationTrackerService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _narrationService = narrationService;
        _serviceProvider = serviceProvider;
        _text = text;
        _narrationUiState = narrationUiState;
        _locationTrackerService = locationTrackerService;

        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _narrationUiState.StateChanged += OnNarrationUiStateChanged;

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyStaticText();
        SetDemoUi(false);

        await _viewModel.InitializeAsync();
        _allPois = (await _viewModel.GetMapPoisAsync()).ToList();

        RefreshMapLocationFromViewModel(true);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void ApplyStaticText()
    {
        Title = "Bản đồ khám phá";
        PageTitleLabel.Text = "Bản đồ khám phá";
        MapBadgeLabel.Text = "Bản đồ";

        if (DemoMenuButtonView is not null)
            DemoMenuButtonView.Text = "Demo";

        if (RealGpsButtonView is not null)
            RealGpsButtonView.Text = "GPS thật";

        NavHomeButton.Text = "Trang chủ";
        NavMapButton.Text = "Bản đồ";
        NavHistoryButton.Text = "Lịch sử";
        NavAccountButton.Text = "Tài khoản";
    }

    private void SetDemoUi(bool isDemoMode, string? label = null)
    {
        if (RealGpsButtonView is not null)
            RealGpsButtonView.IsVisible = isDemoMode;

        if (DemoModeLabelView is not null)
        {
            DemoModeLabelView.IsVisible = isDemoMode;
            DemoModeLabelView.Text = isDemoMode
                ? $"Đang demo: {label}"
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
                RefreshMapSurface(_followMyLocation);
                UpdateNearestPoiInfo();
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
        UpdateNearestPoiInfo();
        RefreshMapSurface(shouldCenterMap);
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
        var map = new Mapsui.Map();
        map.Widgets.Clear();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        var nearestPoiId = GetNearestPoiIdForCurrentLocation();

        _poiLayer = new MemoryLayer("POIs")
        {
            Features = _allPois
                .Select(poi => CreatePoiFeature(poi, nearestPoiId == poi.Id))
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
        map.Layers.Add(currentLocationLayer);

        CenterMap(map);
        return map;
    }

    private void CenterMap(Mapsui.Map map)
    {
        var location = GetMapCenterLocation();
        var center = SphericalMercator
            .FromLonLat(location.Longitude, location.Latitude)
            .ToMPoint();

        var resolutions = map.Navigator.Resolutions;
        var zoomResolution = resolutions.Count > 19 ? resolutions[19] : resolutions[^1];

        map.Navigator.CenterOnAndZoomTo(center, zoomResolution);
    }

    private Location GetMapCenterLocation()
        => _currentGpsLocation ?? new Location(DefaultMapLatitude, DefaultMapLongitude);

    private int? GetNearestPoiIdForCurrentLocation()
    {
        if (_currentGpsLocation is null || _allPois.Count == 0)
            return null;

        Poi? nearestPoi = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var poi in _allPois)
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
        if (_currentGpsLocation is null)
        {
            InfoLabel.Text = "Chưa có vị trí hiện tại.";
            return;
        }

        if (_allPois.Count == 0)
        {
            InfoLabel.Text = $"GPS: {_currentGpsLocation.Latitude:F6}, {_currentGpsLocation.Longitude:F6}";
            return;
        }

        Poi? nearestPoi = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var poi in _allPois)
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

        InfoLabel.Text = nearestPoi is null
            ? $"GPS: {_currentGpsLocation.Latitude:F6}, {_currentGpsLocation.Longitude:F6}"
            : $"Gần nhất: {nearestPoi.Name} • {nearestDistanceMeters:F0} m";
    }

    private static PointFeature CreatePoiFeature(Poi poi, bool isNearest)
    {
        var point = SphericalMercator
            .FromLonLat(poi.Longitude, poi.Latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature["Name"] = poi.Name;
        feature["Address"] = poi.Address;
        feature["PoiId"] = poi.Id;

        feature.Styles.Add(new ImageStyle
        {
            Image = isNearest ? NearestPoiPinSvg : PoiPinSvg,
            SymbolScale = isNearest ? 0.68 : 0.55,
            Offset = new Offset(0, 18),
            RotateWithMap = false
        });

        return feature;
    }

    private PointFeature? CreateCurrentLocationFeature()
    {
        if (_currentGpsLocation is null)
            return null;

        var point = SphericalMercator
            .FromLonLat(_currentGpsLocation.Longitude, _currentGpsLocation.Latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature["Name"] = "Vị trí hiện tại";

        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.20,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Blue),
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
                InfoLabel.Text = "Không lấy được GPS thật.";
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

        var poi = _allPois.FirstOrDefault(x => x.Id == poiId);
        if (poi is null)
            return;

        InfoLabel.Text = $"Đã chọn: {poi.Name}";
        await OpenPoiDetailAsync(poi);
    }

    private async Task OpenPoiDetailAsync(Poi poi)
    {
        await Navigation.PushAsync(new PoiDetailPage(
            poi,
            _narrationService,
            _text,
            _narrationUiState));
    }

    private async void OnDemoMenuClicked(object sender, EventArgs e)
    {
        var choice = await DisplayActionSheetAsync(
            "Chọn điểm demo",
            "Hủy",
            null,
            "Ốc Vũ",
            "Điểm giữa",
            "Ốc Loan");

        if (string.IsNullOrWhiteSpace(choice) || choice == "Hủy")
            return;

        switch (choice)
        {
            case "Ốc Vũ":
                await ActivateDemoPointAsync("Ốc Vũ", OcVuLatitude, OcVuLongitude);
                break;

            case "Điểm giữa":
                await ActivateDemoPointAsync("Điểm giữa", DemoMidLatitude, DemoMidLongitude);
                break;

            case "Ốc Loan":
                await ActivateDemoPointAsync("Ốc Loan", OcLoanLatitude, OcLoanLongitude);
                break;
        }
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
            InfoLabel.Text = "Đã quay về GPS thật.";
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
}