using Mapsui;
using Mapsui.Extensions;
using Mapsui.Features;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.UI.Maui.Extensions;
using Mapsui.Widgets;
using Mapsui.Widgets.InfoWidgets;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class FullMapPage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly NarrationService _narrationService;
    private readonly IServiceProvider _serviceProvider;
    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private MemoryLayer? _currentLocationLayer;

    private Location? _currentGpsLocation;
    private bool _isListeningLocation;
    private bool _followMyLocation = true;

    private const double DemoLatitude = 10.7618;
    private const double DemoLongitude = 106.7022;

    private const string PoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 64 64'>" +
        "<path d='M32 4C20.402 4 11 13.402 11 25c0 14.5 21 35 21 35s21-20.5 21-35C53 13.402 43.598 4 32 4z' fill='#ff1f1f'/>" +
        "<circle cx='32' cy='25' r='10' fill='white'/>" +
        "</svg>";

    public FullMapPage(
    HomeViewModel viewModel,
    NarrationService narrationService,
    IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _narrationService = narrationService;
        _serviceProvider = serviceProvider;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InitializeAsync();
        await InitializeGpsAsync();
        InitializeMap();
        UpdateNearestPoiInfo();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopListeningLocation();
    }

    private async Task InitializeGpsAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (permission != PermissionStatus.Granted)
        {
            InfoLabel.Text = "Chưa được cấp quyền vị trí.";
            return;
        }

        await LoadCurrentLocationAsync();
        await StartListeningLocationAsync();
    }

    private async Task LoadCurrentLocationAsync()
    {
        try
        {
            var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
            if (lastKnown is not null)
                _currentGpsLocation = lastKnown;

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var current = await Geolocation.Default.GetLocationAsync(request);
            if (current is not null)
                _currentGpsLocation = current;

            UpdateNearestPoiInfo();
        }
        catch (FeatureNotSupportedException)
        {
            InfoLabel.Text = "Thiết bị không hỗ trợ GPS.";
        }
        catch (FeatureNotEnabledException)
        {
            InfoLabel.Text = "GPS đang tắt. Hãy bật vị trí trên thiết bị.";
        }
        catch (PermissionException)
        {
            InfoLabel.Text = "Ứng dụng chưa có quyền vị trí.";
        }
        catch
        {
            InfoLabel.Text = "Không lấy được vị trí hiện tại.";
        }
    }

    private async Task StartListeningLocationAsync()
    {
        if (_isListeningLocation)
            return;

        try
        {
            Geolocation.LocationChanged += Geolocation_LocationChanged;

            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best);
            var success = await Geolocation.StartListeningForegroundAsync(request);

            _isListeningLocation = success;

            if (!success)
                InfoLabel.Text = "Không thể bắt đầu theo dõi GPS.";
        }
        catch
        {
            InfoLabel.Text = "Lỗi khi bật theo dõi GPS.";
        }
    }

    private void StopListeningLocation()
    {
        if (!_isListeningLocation)
            return;

        try
        {
            Geolocation.LocationChanged -= Geolocation_LocationChanged;
            Geolocation.StopListeningForeground();
        }
        catch
        {
        }
        finally
        {
            _isListeningLocation = false;
        }
    }

    private async void Geolocation_LocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        _currentGpsLocation = e.Location;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            UpdateNearestPoiInfo();

            if (_mapControl is null)
                return Task.CompletedTask;

            _mapControl.Map = BuildMap();

            if (_followMyLocation)
                CenterMap(_mapControl.Map);

            _mapControl.Refresh();
            return Task.CompletedTask;
        });
    }

    private void InitializeMap()
    {
        LoggingWidget.ShowLoggingInMap = ActiveMode.No;

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

    private Mapsui.Map BuildMap()
    {
        var map = new Mapsui.Map();
        map.Widgets.Clear();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        _poiLayer = new MemoryLayer("POIs")
        {
            Features = _viewModel.NearbyPois
                .Select(CreatePoiFeature)
                .Cast<IFeature>()
                .ToList()
        };

        var currentLocationFeature = CreateCurrentLocationFeature();

        _currentLocationLayer = new MemoryLayer("CurrentLocation")
        {
            Features = currentLocationFeature is null
                ? new List<IFeature>()
                : new List<IFeature> { currentLocationFeature }
        };

        map.Layers.Add(_poiLayer);
        map.Layers.Add(_currentLocationLayer);

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
    {
        if (_currentGpsLocation is not null && IsGpsNearPoiArea())
            return _currentGpsLocation;

        return _currentGpsLocation ?? new Location(DemoLatitude, DemoLongitude);
    }

    private bool IsGpsNearPoiArea()
    {
        if (_currentGpsLocation is null)
            return false;

        const double maxDistanceKm = 3.0;

        if (_viewModel.NearbyPois is not null && _viewModel.NearbyPois.Any())
        {
            foreach (var poi in _viewModel.NearbyPois)
            {
                var distanceKm = Location.CalculateDistance(
                    _currentGpsLocation.Latitude,
                    _currentGpsLocation.Longitude,
                    poi.Latitude,
                    poi.Longitude,
                    DistanceUnits.Kilometers);

                if (distanceKm <= maxDistanceKm)
                    return true;
            }
        }

        return false;
    }

    private void UpdateNearestPoiInfo()
    {
        if (_currentGpsLocation is null)
        {
            InfoLabel.Text = "Chưa lấy được vị trí GPS.";
            return;
        }

        if (_viewModel.NearbyPois is null || !_viewModel.NearbyPois.Any())
        {
            InfoLabel.Text = $"GPS đang hoạt động • {_currentGpsLocation.Latitude:F4}, {_currentGpsLocation.Longitude:F4}";
            return;
        }

        Poi? nearestPoi = null;
        double nearestDistanceMeters = double.MaxValue;

        foreach (var poi in _viewModel.NearbyPois)
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
            InfoLabel.Text = $"GPS đang hoạt động • {_currentGpsLocation.Latitude:F4}, {_currentGpsLocation.Longitude:F4}";
            return;
        }

        InfoLabel.Text = $"Gần nhất: {nearestPoi.Name} • {nearestDistanceMeters:F0} m";
    }

    private static PointFeature CreatePoiFeature(Poi poi)
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
            Image = PoiPinSvg,
            SymbolScale = 0.55,
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

        _followMyLocation = true;
        await LoadCurrentLocationAsync();

        _mapControl.Map = BuildMap();
        _mapControl.Refresh();
    }

    private async void OnMapTapped(object? sender, MapEventArgs e)
    {
        _followMyLocation = false;

        if (_mapControl?.Map is null)
            return;

        if (_poiLayer is not null)
        {
            var mapInfo = e.GetMapInfo(new[] { _poiLayer });

            if (mapInfo?.Feature is not null)
            {
                var name = mapInfo.Feature["Name"]?.ToString();
                var poiIdObj = mapInfo.Feature["PoiId"];

                if (!string.IsNullOrWhiteSpace(name))
                    InfoLabel.Text = $"Quán: {name}";

                if (poiIdObj is not null && int.TryParse(poiIdObj.ToString(), out var poiId))
                {
                    await _narrationService.PlayPoiAsync(poiId);
                }

                return;
            }
        }

        var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
        _currentGpsLocation = new Location(lat, lon);

        UpdateNearestPoiInfo();

        _mapControl.Map = BuildMap();
        _mapControl.Refresh();
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
        await DisplayAlert("Thông báo", "Chức năng người dùng sẽ làm tiếp.", "OK");
    }
}