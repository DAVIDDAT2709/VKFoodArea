using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class TourCatalogPage : ContentPage
{
    private readonly TourCatalogService _tourCatalogService;
    private readonly TourSessionService _tourSessionService;
    private readonly TourNarrationService _tourNarrationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;

    private readonly Label _headerTitleLabel;
    private readonly Label _headerSubtitleLabel;
    private readonly Label _activeSessionCardTitleLabel;
    private readonly Label _activeSessionTitleLabel;
    private readonly Label _activeSessionStatusLabel;
    private readonly Border _activeSessionCard;
    private readonly Button _openCurrentButton;
    private readonly Button _cancelCurrentButton;
    private readonly Button _refreshButton;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Label _statusLabel;
    private readonly VerticalStackLayout _tourListLayout;

    private bool _isLoading;

    public TourCatalogPage(
        TourCatalogService tourCatalogService,
        TourSessionService tourSessionService,
        TourNarrationService tourNarrationService,
        IServiceProvider serviceProvider,
        AppTextService text)
    {
        _tourCatalogService = tourCatalogService;
        _tourSessionService = tourSessionService;
        _tourNarrationService = tourNarrationService;
        _serviceProvider = serviceProvider;
        _text = text;

        BackgroundColor = Color.FromArgb("#F5F7F6");

        _headerTitleLabel = new Label
        {
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#173330")
        };
        _headerSubtitleLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#617A74")
        };
        _activeSessionCardTitleLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F6F64")
        };
        _activeSessionTitleLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#173330")
        };
        _activeSessionStatusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#617A74")
        };

        _openCurrentButton = new Button
        {
            BackgroundColor = Color.FromArgb("#1F6F64"),
            TextColor = Colors.White,
            CornerRadius = 14,
            Padding = new Thickness(14, 10)
        };
        _openCurrentButton.Clicked += OnOpenCurrentTourClicked;

        _cancelCurrentButton = new Button
        {
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#B8452E"),
            BorderColor = Color.FromArgb("#E7B8AE"),
            BorderWidth = 1,
            CornerRadius = 14,
            Padding = new Thickness(14, 10)
        };
        _cancelCurrentButton.Clicked += OnCancelCurrentTourClicked;

        _activeSessionCard = new Border
        {
            IsVisible = false,
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#D8E2DE"),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    _activeSessionCardTitleLabel,
                    _activeSessionTitleLabel,
                    _activeSessionStatusLabel,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            _openCurrentButton,
                            _cancelCurrentButton
                        }
                    }
                }
            }
        };

        _refreshButton = new Button
        {
            BackgroundColor = Color.FromArgb("#EAF4F1"),
            TextColor = Color.FromArgb("#1F6F64"),
            CornerRadius = 14,
            Padding = new Thickness(14, 10),
            HorizontalOptions = LayoutOptions.Start
        };
        _refreshButton.Clicked += OnRefreshClicked;

        _loadingIndicator = new ActivityIndicator
        {
            IsVisible = false,
            IsRunning = false,
            Color = Color.FromArgb("#1F6F64"),
            HeightRequest = 32,
            WidthRequest = 32
        };

        _statusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#617A74"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _tourListLayout = new VerticalStackLayout
        {
            Spacing = 12
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 14,
                Children =
                {
                    _headerTitleLabel,
                    _headerSubtitleLabel,
                    _activeSessionCard,
                    _refreshButton,
                    _loadingIndicator,
                    _statusLabel,
                    _tourListLayout
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();
        RefreshActiveSessionCard();
        await LoadToursAsync();
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Tour.PageTitle"];
        _headerTitleLabel.Text = _text["Tour.CatalogTitle"];
        _headerSubtitleLabel.Text = _text["Tour.CatalogSubtitle"];
        _activeSessionCardTitleLabel.Text = _text["Tour.CurrentCardTitle"];
        _openCurrentButton.Text = _text["Tour.OpenCurrent"];
        _cancelCurrentButton.Text = _text["Tour.EndCurrent"];
        _refreshButton.Text = _text["Tour.RefreshList"];
    }

    private async Task LoadToursAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;
        _statusLabel.Text = _text["Tour.Loading"];

        try
        {
            var tours = await _tourCatalogService.GetActiveToursAsync();
            RenderTours(tours);
            _statusLabel.Text = tours.Count == 0
                ? _text["Tour.EmptyActive"]
                : _text.Format("Tour.LoadedCount", tours.Count);
        }
        catch (Exception ex)
        {
            _tourListLayout.Children.Clear();
            _statusLabel.Text = _text.Format("Tour.LoadError", ex.Message);
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
            _isLoading = false;
        }
    }

    private void RenderTours(IReadOnlyList<Tour> tours)
    {
        _tourListLayout.Children.Clear();

        if (tours.Count == 0)
        {
            _tourListLayout.Children.Add(new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#D8E2DE"),
                StrokeThickness = 1,
                Padding = new Thickness(16),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Content = new Label
                {
                    Text = _text["Tour.EmptyList"],
                    FontSize = 14,
                    TextColor = Color.FromArgb("#617A74")
                }
            });
            return;
        }

        foreach (var tour in tours)
            _tourListLayout.Children.Add(BuildTourCard(tour));
    }

    private View BuildTourCard(Tour tour)
    {
        var orderedStops = tour.Stops
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        var routePreview = orderedStops.Count == 0
            ? _text["Tour.NoRoute"]
            : string.Join(" -> ", orderedStops
                .Select(x => x.Poi?.Name ?? $"POI #{x.PoiId}")
                .Take(4));

        if (orderedStops.Count > 4)
            routePreview = _text.Format("Tour.RouteOverflow", routePreview, orderedStops.Count - 4);

        var firstStop = orderedStops.FirstOrDefault();
        var firstStopName = firstStop?.Poi?.Name
                            ?? (firstStop is not null
                                ? $"POI #{firstStop.PoiId}"
                                : _text["Tour.FirstStopNone"]);

        var startButton = new Button
        {
            Text = _text["Tour.StartThisTour"],
            BackgroundColor = Color.FromArgb("#173330"),
            TextColor = Colors.White,
            CornerRadius = 14,
            Padding = new Thickness(14, 10)
        };
        startButton.Clicked += async (_, _) => await StartTourAsync(tour);

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#D8E2DE"),
            StrokeThickness = 1,
            Padding = new Thickness(16),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = tour.Name,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#173330")
                    },
                    new Label
                    {
                        Text = ResolveTourSummary(tour),
                        FontSize = 13,
                        TextColor = Color.FromArgb("#617A74")
                    },
                    new Label
                    {
                        Text = _text.Format("Tour.StopCountSummary", orderedStops.Count, firstStopName),
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1F6F64")
                    },
                    new Label
                    {
                        Text = _text.Format("Tour.RouteSummary", routePreview),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#48635F")
                    },
                    startButton
                }
            }
        };
    }

    private string ResolveTourSummary(Tour tour)
    {
        var summary = _tourNarrationService.ResolveDisplaySummary(tour);
        return string.IsNullOrWhiteSpace(summary)
            ? _text["Tour.NoDescription"]
            : summary;
    }

    private async Task StartTourAsync(Tour tour)
    {
        var currentSession = _tourSessionService.GetCurrentSession();
        if (currentSession is not null &&
            !currentSession.IsFinished &&
            currentSession.TourId != tour.Id)
        {
            var replace = await DisplayAlertAsync(
                _text["Tour.ReplaceTitle"],
                _text.Format("Tour.ReplaceMessage", currentSession.TourName, tour.Name),
                _text["Tour.ReplaceConfirm"],
                _text["Common.Cancel"]);

            if (!replace)
                return;
        }

        _tourSessionService.Start(tour);
        RefreshActiveSessionCard();
        await Navigation.PushAsync(_serviceProvider.GetRequiredService<FullMapPage>());
    }

    private void RefreshActiveSessionCard()
    {
        var session = _tourSessionService.GetCurrentSession();
        _activeSessionCard.IsVisible = session is not null;

        if (session is null)
        {
            _activeSessionTitleLabel.Text = string.Empty;
            _activeSessionStatusLabel.Text = string.Empty;
            return;
        }

        _activeSessionTitleLabel.Text = session.TourName;
        _activeSessionStatusLabel.Text = session.IsFinished
            ? _text["Tour.ActiveFinished"]
            : _text.Format(
                "Tour.ActiveRouting",
                session.CurrentStop?.Poi?.Name ?? $"POI #{session.CurrentStop?.PoiId}");
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadToursAsync();
    }

    private async void OnOpenCurrentTourClicked(object? sender, EventArgs e)
    {
        if (_tourSessionService.GetCurrentSession() is null)
            return;

        await Navigation.PushAsync(_serviceProvider.GetRequiredService<FullMapPage>());
    }

    private async void OnCancelCurrentTourClicked(object? sender, EventArgs e)
    {
        if (_tourSessionService.GetCurrentSession() is null)
            return;

        var confirmed = await DisplayAlertAsync(
            _text["Tour.CancelTitle"],
            _text["Tour.CancelMessage"],
            _text["Tour.ReplaceConfirm"],
            _text["Common.Cancel"]);

        if (!confirmed)
            return;

        _tourSessionService.Cancel();
        RefreshActiveSessionCard();
    }
}
