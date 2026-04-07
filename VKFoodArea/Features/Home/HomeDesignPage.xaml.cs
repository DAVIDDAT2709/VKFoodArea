using System.Globalization;
using System.Text;
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
    private readonly PoiRepository _poiRepository;
    private readonly FoodRepository _foodRepository;
    private readonly Dictionary<int, PoiSearchDescriptor> _poiSearchCache = new();

    private DateTime _lastMapTapTime = DateTime.MinValue;
    private bool _isOpeningFullMap;
    private bool _isApplyingSuggestion;

    private MapControl? _mapControl;
    private MemoryLayer? _poiLayer;
    private MemoryLayer? _currentLocationLayer;

    private List<Poi> _allPois = new();
    private List<Poi> _defaultPois = new();
    private List<Poi> _displayedPois = new();

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
        PoiRepository poiRepository,
        FoodRepository foodRepository,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _narrationService = narrationService;
        _settingsPage = settingsPage;
        _fullMapPage = fullMapPage;
        _poiRepository = poiRepository;
        _foodRepository = foodRepository;
        _serviceProvider = serviceProvider;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        PoiSyncService.SyncCompleted -= OnSyncCompleted;
        PoiSyncService.SyncCompleted += OnSyncCompleted;
        if (Window is not null)
        {
            Window.Resumed -= OnWindowResumed;
            Window.Resumed += OnWindowResumed;
        }

        await _viewModel.InitializeAsync();
        _viewModel.RefreshNarrationSettings();
        await RefreshPoiDataAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        PoiSyncService.SyncCompleted -= OnSyncCompleted;
        if (Window is not null)
            Window.Resumed -= OnWindowResumed;
    }

    private void OnSyncCompleted(object? sender, PoiSyncService.PoiSyncCompletedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var detail = e.Result.Success
                ? $"Đồng bộ xong, hiện có {e.Result.RemoteCount} POI từ web."
                : "Không kết nối web, đang hiển thị dữ liệu local.";

            await _viewModel.RefreshVisiblePoisAsync(e.Result, detail);
            await RefreshPoiDataAsync();
        });
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _viewModel.InitializeAsync();
            await RefreshPoiDataAsync();
        });
    }

    private async Task RefreshPoiDataAsync()
    {
        _allPois = await _poiRepository.GetActiveAsync();
        _defaultPois = _viewModel.NearbyPois.ToList();

        if (_allPois.Count == 0)
            _allPois = _defaultPois.ToList();

        if (_defaultPois.Count == 0)
            _defaultPois = _allPois.Take(10).ToList();

        RebuildPoiSearchCache();
        ApplySearch(PoiSearchBar.Text, closeSuggestions: true);
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

        _mapControl.Map = BuildMap(_displayedPois);
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

        await DisplayAlert("Quan an", name, "OK");
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
        if (_isApplyingSuggestion)
            return;

        UpdateSuggestions(e.NewTextValue);
        ApplySearch(e.NewTextValue, closeSuggestions: false);
    }

    private void OnSearchButtonPressed(object sender, EventArgs e)
    {
        ApplySearch(PoiSearchBar.Text, closeSuggestions: true);
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

        ApplySearch(suggestion.Name, closeSuggestions: true);
        await OpenPoiDetailAsync(suggestion.Poi);
    }

    private void UpdateSuggestions(string? keyword)
    {
        var suggestions = GetSuggestions(keyword);

        SearchSuggestionContainer.IsVisible = suggestions.Count > 0;
        SearchSuggestionCollectionView.ItemsSource = suggestions;
        SearchSuggestionCollectionView.HeightRequest = suggestions.Count switch
        {
            <= 0 => 0,
            > 5 => 280,
            _ => suggestions.Count * 56
        };
    }

    private List<HomePoiSuggestion> GetSuggestions(string? keyword)
    {
        var normalizedKeyword = NormalizeSearchText(keyword);

        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return [];

        return _allPois
            .Select(poi => new
            {
                Poi = poi,
                Score = GetSearchScore(poi, normalizedKeyword)
            })
            .Where(x => x.Score > int.MinValue)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Poi.Name.Length)
            .Take(6)
            .Select(x => new HomePoiSuggestion
            {
                Poi = x.Poi
            })
            .ToList();
    }

    private void ApplySearch(string? keyword, bool closeSuggestions)
    {
        var normalizedKeyword = NormalizeSearchText(keyword);

        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            _displayedPois = _defaultPois.ToList();
        }
        else
        {
            _displayedPois = _allPois
                .Select(poi => new
                {
                    Poi = poi,
                    Score = GetSearchScore(poi, normalizedKeyword)
                })
                .Where(x => x.Score > int.MinValue)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Poi.Name.Length)
                .Select(x => x.Poi)
                .ToList();
        }

        PoiCollectionView.ItemsSource = _displayedPois;

        if (_mapControl is not null)
        {
            _mapControl.Map = BuildMap(_displayedPois);
            _mapControl.Refresh();
        }

        if (closeSuggestions || string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            SearchSuggestionContainer.IsVisible = false;
            SearchSuggestionCollectionView.ItemsSource = null;
            SearchSuggestionCollectionView.HeightRequest = 0;
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

        await OpenPoiDetailAsync(poi);
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
        await DisplayAlert("Thong bao", "Chuc nang nguoi dung se lam tiep.", "OK");
    }

    private Task OpenPoiDetailAsync(Poi poi)
    {
        return Navigation.PushAsync(new PoiDetailPage(poi, _narrationService, _foodRepository));
    }

    private void RebuildPoiSearchCache()
    {
        _poiSearchCache.Clear();

        foreach (var poi in _allPois)
        {
            _poiSearchCache[poi.Id] = new PoiSearchDescriptor(
                NormalizeSearchText(poi.Name),
                NormalizeSearchText(poi.Address));
        }
    }

    private int GetSearchScore(Poi poi, string normalizedKeyword)
    {
        if (!_poiSearchCache.TryGetValue(poi.Id, out var descriptor))
            return int.MinValue;

        var tokens = normalizedKeyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            return int.MinValue;

        var combined = $"{descriptor.NameKey} {descriptor.AddressKey}";

        if (tokens.Any(token => !combined.Contains(token, StringComparison.Ordinal)))
            return int.MinValue;

        var score = 0;

        if (descriptor.NameKey.StartsWith(normalizedKeyword, StringComparison.Ordinal))
            score += 500;
        else if (descriptor.NameKey.Contains(normalizedKeyword, StringComparison.Ordinal))
            score += 360;
        else if (descriptor.AddressKey.StartsWith(normalizedKeyword, StringComparison.Ordinal))
            score += 260;
        else if (descriptor.AddressKey.Contains(normalizedKeyword, StringComparison.Ordinal))
            score += 180;

        foreach (var token in tokens)
        {
            if (descriptor.NameKey.Contains(token, StringComparison.Ordinal))
                score += 30;
            else if (descriptor.AddressKey.Contains(token, StringComparison.Ordinal))
                score += 16;
        }

        return score;
    }

    private static string NormalizeSearchText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => character
            });
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private sealed record PoiSearchDescriptor(string NameKey, string AddressKey);
}
