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
using VKFoodArea.Features.Settings;
using VKFoodArea.Features.User;
using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class HomeDesignPage : ContentPage
{
    private readonly HomeViewModel _viewModel;
    private readonly NarrationService _narrationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly FoodRepository _foodRepository;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;

    private DateTime _lastMapTapTime = DateTime.MinValue;
    private bool _isOpeningFullMap;
    private bool _isApplyingSuggestion;

    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private MemoryLayer? _currentLocationLayer;

    private List<FeaturedFoodCardViewModel> _featuredFoodCards = new();

    private const string PoiPinSvg =
        "svg-content://<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 64 64'>" +
        "<path d='M32 4C20.402 4 11 13.402 11 25c0 14.5 21 35 21 35s21-20.5 21-35C53 13.402 43.598 4 32 4z' fill='#ff1f1f'/>" +
        "<circle cx='32' cy='25' r='10' fill='white'/>" +
        "</svg>";

    public HomeDesignPage(
        HomeViewModel viewModel,
        NarrationService narrationService,
        FoodRepository foodRepository,
        IServiceProvider serviceProvider,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _narrationService = narrationService;
        _foodRepository = foodRepository;
        _serviceProvider = serviceProvider;
        _text = text;
        _narrationUiState = narrationUiState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PoiSyncService.SyncCompleted -= OnSyncCompletedEscaped;
        PoiSyncService.SyncCompleted += OnSyncCompletedEscaped;
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        _narrationUiState.StateChanged += OnNarrationUiStateChanged;
        if (Window is not null)
        {
            Window.Resumed -= OnWindowResumed;
            Window.Resumed += OnWindowResumed;
        }

        ApplyLocalizedTextClean();
        await _viewModel.InitializeAsync();
        _viewModel.RefreshNarrationSettings();
        await RefreshPoiDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        PoiSyncService.SyncCompleted -= OnSyncCompletedEscaped;
        _narrationUiState.StateChanged -= OnNarrationUiStateChanged;
        if (Window is not null)
            Window.Resumed -= OnWindowResumed;
    }

    private void OnSyncCompletedEscaped(object? sender, PoiSyncService.PoiSyncCompletedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var detail = e.Result.Success
                ? _text.Format("Status.SyncCompleted", e.Result.RemoteCount)
                : _text["Status.UsingLocalData"];

            await _viewModel.RefreshVisiblePoisAsync(e.Result, detail);
            await RefreshPoiDataAsync();
        });
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ApplyLocalizedTextClean();
            await _viewModel.InitializeAsync();
            await RefreshPoiDataAsync();
        });
    }

    private async Task RefreshPoiDataAsync()
    {
        await LoadFeaturedFoodsAsync();
        InitializeMap();
    }

    private async Task LoadFeaturedFoodsAsync()
    {
        _featuredFoodCards = (await _foodRepository.GetByCategoryAsync("Recommended"))
            .Take(5)
            .Select(food => new FeaturedFoodCardViewModel
            {
                Food = food,
                PriceText = _text.Format("Home.PriceFrom", food.Price)
            })
            .ToList();

        FeaturedFoodCollectionView.ItemsSource = _featuredFoodCards;
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

        _mapControl.Map = BuildMap(_viewModel.DisplayedPois);
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
            Features =
            [
                CreateCurrentLocationFeature()
            ]
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
        feature["Name"] = "Vi tri hien tai";

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
                await Navigation.PushAsync(_serviceProvider.GetRequiredService<FullMapPage>());
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

        if (!int.TryParse(mapInfo.Feature["PoiId"]?.ToString(), out var poiId))
            return;

        var poi = _viewModel.FindLoadedPoiById(poiId);
        if (poi is null)
            return;

        await OpenPoiDetailAsync(poi);
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

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingSuggestion)
            return;

        await _viewModel.SearchPoisAsync(e.NewTextValue, closeSuggestions: false);
        RefreshMapPois();
    }

    private async void OnSearchButtonPressed(object sender, EventArgs e)
    {
        await _viewModel.SearchPoisAsync(PoiSearchBar.Text, closeSuggestions: true);
        RefreshMapPois();
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        var suggestion = e.CurrentSelection.FirstOrDefault() as HomePoiSuggestion;
        SearchSuggestionCollectionView.SelectedItem = null;

        if (suggestion is null)
            return;

        _isApplyingSuggestion = true;
        PoiSearchBar.Text = suggestion.Name;
        _isApplyingSuggestion = false;

        await _viewModel.SearchPoisAsync(suggestion.Name, closeSuggestions: true);
        RefreshMapPois();
        await OpenPoiDetailAsync(suggestion.Poi);
    }

    private async void OnOpenFullMapClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_serviceProvider.GetRequiredService<FullMapPage>());
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_serviceProvider.GetRequiredService<SettingsPage>());
    }

    private async void OnPoiCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Grid grid)
            return;

        if (grid.BindingContext is not Poi poi)
            return;

        await OpenPoiDetailAsync(poi);
    }

    private async void OnFeaturedFoodTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border border || border.BindingContext is not FeaturedFoodCardViewModel card)
            return;

        var food = card.Food;
        var poi = _viewModel.FindPoiByRestaurantName(food.RestaurantName);

        if (poi is not null)
        {
            await OpenPoiDetailAsync(poi);
            return;
        }

        await DisplayAlertAsync(
            _text["Home.FeaturedFoodAlertTitle"],
            $"{food.Name}\n{food.RestaurantName}",
            _text["Common.Ok"]);
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
        var page = _serviceProvider.GetRequiredService<UserPage>();
        await Navigation.PushAsync(page);
    }

    private Task OpenPoiDetailAsync(Poi poi)
    {
        return Navigation.PushAsync(new PoiDetailPage(
            poi,
            _narrationService,
            _text,
            _narrationUiState));
    }

    private void OnNarrationUiStateChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(_viewModel.RefreshNarrationState);
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Nav.Home"];
        PoiSearchBar.Placeholder = _text["Home.SearchPlaceholder"];
        SearchEmptyStateLabel.Text = _text["Home.SearchEmptyMessage"];
        QrButton.Text = _text["Home.QrButton"];
        FeaturedFoodsTitleLabel.Text = _text["Home.FeaturedFoods"];
        FeaturedPoisTitleLabel.Text = _text["Home.FeaturedPois"];
        HistoryActionButton.Text = _text["Common.History"];
        MiniPlayerStopButton.Text = _text["Common.Stop"];
        NavHomeButton.Text = $"🏠\n{_text["Nav.Home"]}";
        NavMapButton.Text = $"🗺\n{_text["Nav.Map"]}";
        NavHistoryButton.Text = $"🕘\n{_text["Nav.History"]}";
        NavAccountButton.Text = $"👤\n{_text["Nav.Account"]}";
        _viewModel.RefreshLocalizedText();
    }

    private void RefreshMapPois()
    {
        if (_mapControl is null)
            return;

        _mapControl.Map = BuildMap(_viewModel.DisplayedPois);
        _mapControl.Refresh();
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
