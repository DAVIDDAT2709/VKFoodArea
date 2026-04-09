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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using VKFoodArea.Features.User;
using VKFoodArea.Models;
using VKFoodArea.Services;
using System.ComponentModel;

namespace VKFoodArea.Features.Home;

public partial class FullMapPage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private List<Poi> _allPois = new();

    private Location? _currentGpsLocation;
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
        IServiceProvider serviceProvider,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _text = text;
        _narrationUiState = narrationUiState;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _narrationUiState.StateChanged += OnNarrationUiStateChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyLocalizedTextClean();
        await _viewModel.InitializeAsync();
        _allPois = (await _viewModel.GetMapPoisAsync()).ToList();
        RefreshMapLocationFromViewModel(shouldCenterMap: true);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeViewModel.CurrentLocation))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
            RefreshMapLocationFromViewModel(_followMyLocation));
    }

    private void RefreshMapLocationFromViewModel(bool shouldCenterMap)
    {
        _currentGpsLocation = _viewModel.CurrentLocation;
        UpdateNearestPoiInfo();

        if (_mapControl is null)
        {
            InitializeMap();
            return;
        }

        _mapControl.Map = BuildMap();

        if (shouldCenterMap)
            CenterMap(_mapControl.Map);

        _mapControl.Refresh();
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
            Features = _allPois
                .Select(CreatePoiFeature)
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
        => _currentGpsLocation ?? new Location(DemoLatitude, DemoLongitude);

    private void UpdateNearestPoiInfo()
    {
        if (_currentGpsLocation is null)
        {
            InfoLabel.Text = _text["Map.InfoLocationUnavailable"];
            return;
        }

        if (_allPois.Count == 0)
        {
            InfoLabel.Text = _text.Format(
                "Map.InfoGpsActiveCoords",
                _currentGpsLocation.Latitude,
                _currentGpsLocation.Longitude);
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

        if (nearestPoi is null)
        {
            InfoLabel.Text = _text.Format(
                "Map.InfoGpsActiveCoords",
                _currentGpsLocation.Latitude,
                _currentGpsLocation.Longitude);
            return;
        }

        InfoLabel.Text = _text.Format("Map.InfoNearestDistance", nearestPoi.Name, nearestDistanceMeters);
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

        if (!await _viewModel.RefreshCurrentLocationAsync())
        {
            if (_currentGpsLocation is null)
                InfoLabel.Text = _text["Map.InfoLocationUnavailable"];

            return;
        }

        RefreshMapLocationFromViewModel(shouldCenterMap: true);
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

        var wasPlayingCurrentPoi = _viewModel.IsPoiNarrationPlaying(poi.Id);
        await _viewModel.PlayPoiAudioAsync(poi);

        InfoLabel.Text = wasPlayingCurrentPoi
            ? _text.Format("Map.InfoStoppedNarration", poi.Name)
            : _text.Format("Map.InfoRestaurant", poi.Name);
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

    private void ApplyLocalizedText()
    {
        Title = _text["Map.PageTitle"];
        PageTitleLabel.Text = _text["Map.PageTitle"];
        MapBadgeLabel.Text = _text["Nav.Map"];
        MiniPlayerStopButton.Text = _text["Common.Stop"];
        NavHomeButton.Text = $"🏠\n{_text["Nav.Home"]}";
        NavMapButton.Text = $"🗺\n{_text["Nav.Map"]}";
        NavHistoryButton.Text = $"🕘\n{_text["Nav.History"]}";
        NavAccountButton.Text = $"👤\n{_text["Nav.Account"]}";
        _viewModel.RefreshLocalizedText();

        if (_currentGpsLocation is null)
            InfoLabel.Text = _text["Map.InfoLocationUnavailable"];
        else
            UpdateNearestPoiInfo();
    }

    private void ApplyLocalizedTextClean()
    {
        ApplyLocalizedText();
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }
}
