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
using VKFoodArea.Features.Settings;
using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class HomeDesignPage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly NarrationService _narrationService;
    private readonly SettingsPage _settingsPage;
    private readonly FullMapPage _fullMapPage;
    private readonly IServiceProvider _serviceProvider;
    private readonly FoodRepository _foodRepository;

    private DateTime _lastMapTapTime = DateTime.MinValue;
    private bool _isOpeningFullMap;

    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private MemoryLayer? _currentLocationLayer;
    private List<Poi> _allPois = new();

    private const string PoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 64 64'>" +
        "<path d='M32 4C20.402 4 11 13.402 11 25c0 14.5 21 35 21 35s21-20.5 21-35C53 13.402 43.598 4 32 4z' fill='#ff1f1f'/>" +
        "<circle cx='32' cy='25' r='10' fill='white'/>" +
        "</svg>";

    public HomeDesignPage(
    HomeViewModel viewModel,
    NarrationService narrationService,
    SettingsPage settingsPage,
    FullMapPage fullMapPage,
    FoodRepository foodRepository,
    IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _narrationService = narrationService;
        _settingsPage = settingsPage;
        _fullMapPage = fullMapPage;
        _foodRepository = foodRepository;
        _serviceProvider = serviceProvider;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.InitializeAsync();
        _viewModel.RefreshNarrationSettings();

        _allPois = _viewModel.NearbyPois.ToList();
        PoiCollectionView.ItemsSource = _allPois;

        InitializeMap();
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

        _mapControl.Map = BuildMap(_allPois);
        _mapControl.Refresh();
    }

    private Mapsui.Map BuildMap(IEnumerable<Poi> pois)
    {
        var map = new Mapsui.Map();
        map.Widgets.Clear();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        _poiLayer = new MemoryLayer("POIs")
        {
            Features = pois
                .Select(CreatePoiFeature)
                .Cast<IFeature>()
                .ToList()
        };

        _currentLocationLayer = new MemoryLayer("CurrentLocation")
        {
            Features = new List<IFeature>
            {
                CreateCurrentLocationFeature()
            }
        };

        map.Layers.Add(_poiLayer);
        map.Layers.Add(_currentLocationLayer);

        CenterOnCurrentLocation(map);

        return map;
    }

    private void CenterOnCurrentLocation(Mapsui.Map map)
    {
        var center = SphericalMercator
            .FromLonLat(_viewModel.CurrentLocation.Longitude, _viewModel.CurrentLocation.Latitude)
            .ToMPoint();

        var resolutions = map.Navigator.Resolutions;
        var zoomResolution = resolutions.Count > 18 ? resolutions[18] : resolutions[^1];

        map.Navigator.CenterOnAndZoomTo(center, zoomResolution);
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
            SymbolScale = 0.52,
            Offset = new Offset(0, 18),
            RotateWithMap = false
        });

        return feature;
    }

    private PointFeature CreateCurrentLocationFeature()
    {
        var point = SphericalMercator
            .FromLonLat(_viewModel.CurrentLocation.Longitude, _viewModel.CurrentLocation.Latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature["Name"] = "Vị trí hiện tại";

        feature.Styles.Add(new SymbolStyle
        {
            SymbolType = SymbolType.Ellipse,
            SymbolScale = 0.18,
            Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Blue),
            Outline = new Pen(Mapsui.Styles.Color.White, 2)
        });

        return feature;
    }

    private async void OnMapTapped(object? sender, MapEventArgs e)
    {
        var now = DateTime.UtcNow;
        var isDoubleTap = (now - _lastMapTapTime).TotalMilliseconds <= 350;
        _lastMapTapTime = now;

        if (isDoubleTap && !_isOpeningFullMap)
        {
            _isOpeningFullMap = true;
            try
            {
                await Navigation.PushAsync(_fullMapPage);
                return;
            }
            finally
            {
                await Task.Delay(300);
                _isOpeningFullMap = false;
            }
        }

        if (_poiLayer is null)
            return;

        var mapInfo = e.GetMapInfo(new[] { _poiLayer });

        if (mapInfo?.Feature is null)
            return;

        var name = mapInfo.Feature["Name"]?.ToString();

        if (string.IsNullOrWhiteSpace(name))
            return;

        await DisplayAlert("Quán ăn", name, "OK");
    }

    private void OnZoomInClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        _mapControl.Map.Navigator.ZoomIn();
        _mapControl.Refresh();
    }

    private void OnZoomOutClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        _mapControl.Map.Navigator.ZoomOut();
        _mapControl.Refresh();
    }

    private void OnMyLocationClicked(object sender, EventArgs e)
    {
        if (_mapControl?.Map is null)
            return;

        CenterOnCurrentLocation(_mapControl.Map);
        _mapControl.Refresh();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySearch(e.NewTextValue);
    }

    private void OnSearchButtonPressed(object sender, EventArgs e)
    {
        ApplySearch(PoiSearchBar.Text);
    }

    private void ApplySearch(string? keyword)
    {
        IEnumerable<Poi> filtered;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            filtered = _allPois;
        }
        else
        {
            var search = keyword.Trim().ToLowerInvariant();

            filtered = _allPois.Where(p =>
                (!string.IsNullOrWhiteSpace(p.Name) && p.Name.ToLowerInvariant().Contains(search)) ||
                (!string.IsNullOrWhiteSpace(p.Address) && p.Address.ToLowerInvariant().Contains(search)));
        }

        var result = filtered.ToList();
        PoiCollectionView.ItemsSource = result;

        if (_mapControl is not null)
        {
            _mapControl.Map = BuildMap(result);
            _mapControl.Refresh();
        }
    }

    private async void OnOpenFullMapClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_fullMapPage);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_settingsPage);
    }

    private async void OnPoiCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Grid grid)
            return;

        if (grid.BindingContext is not Poi poi)
            return;

        await Navigation.PushAsync(new PoiDetailPage(poi, _narrationService, _foodRepository));
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        await Task.CompletedTask;
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<HistoryPage>();
        await Navigation.PushAsync(page);
    }
    private async void OnQrClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<QrScannerPage>();
        await Navigation.PushAsync(page);
    }
    private async void OnUserClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Thông báo", "Chức năng người dùng sẽ làm tiếp.", "OK");
    }
}