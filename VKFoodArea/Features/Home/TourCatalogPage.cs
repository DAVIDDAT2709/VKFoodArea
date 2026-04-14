using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class TourCatalogPage : ContentPage
{
    private readonly TourCatalogService _tourCatalogService;
    private readonly TourSessionService _tourSessionService;
    private readonly IServiceProvider _serviceProvider;

    private readonly Label _activeSessionTitleLabel;
    private readonly Label _activeSessionStatusLabel;
    private readonly Border _activeSessionCard;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Label _statusLabel;
    private readonly VerticalStackLayout _tourListLayout;

    private bool _isLoading;

    public TourCatalogPage(
        TourCatalogService tourCatalogService,
        TourSessionService tourSessionService,
        IServiceProvider serviceProvider)
    {
        _tourCatalogService = tourCatalogService;
        _tourSessionService = tourSessionService;
        _serviceProvider = serviceProvider;

        Title = "Chon tour";
        BackgroundColor = Color.FromArgb("#F5F7F6");

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

        var openCurrentButton = new Button
        {
            Text = "Mo tour dang chay",
            BackgroundColor = Color.FromArgb("#1F6F64"),
            TextColor = Colors.White,
            CornerRadius = 14,
            Padding = new Thickness(14, 10)
        };
        openCurrentButton.Clicked += OnOpenCurrentTourClicked;

        var cancelCurrentButton = new Button
        {
            Text = "Ket thuc tour",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#B8452E"),
            BorderColor = Color.FromArgb("#E7B8AE"),
            BorderWidth = 1,
            CornerRadius = 14,
            Padding = new Thickness(14, 10)
        };
        cancelCurrentButton.Clicked += OnCancelCurrentTourClicked;

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
                    new Label
                    {
                        Text = "Tour hien tai",
                        FontSize = 13,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1F6F64")
                    },
                    _activeSessionTitleLabel,
                    _activeSessionStatusLabel,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            openCurrentButton,
                            cancelCurrentButton
                        }
                    }
                }
            }
        };

        var refreshButton = new Button
        {
            Text = "Lam moi danh sach",
            BackgroundColor = Color.FromArgb("#EAF4F1"),
            TextColor = Color.FromArgb("#1F6F64"),
            CornerRadius = 14,
            Padding = new Thickness(14, 10),
            HorizontalOptions = LayoutOptions.Start
        };
        refreshButton.Clicked += OnRefreshClicked;

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
                    new Label
                    {
                        Text = "Danh sach tour tu web",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#173330")
                    },
                    new Label
                    {
                        Text = "Chon goi tour de app nhan diem dung dau tien va dan GPS theo thu tu stop.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#617A74")
                    },
                    _activeSessionCard,
                    refreshButton,
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
        RefreshActiveSessionCard();
        await LoadToursAsync();
    }

    private async Task LoadToursAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;
        _statusLabel.Text = "Dang tai tour tu web...";

        try
        {
            var tours = await _tourCatalogService.GetActiveToursAsync();
            RenderTours(tours);
            _statusLabel.Text = tours.Count == 0
                ? "Web chua co tour dang hoat dong."
                : $"Da tai {tours.Count} tour dang hoat dong.";
        }
        catch (Exception ex)
        {
            _tourListLayout.Children.Clear();
            _statusLabel.Text = $"Khong tai duoc danh sach tour: {ex.Message}";
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
                    Text = "Chua co tour nao de chon.",
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
            ? "Chua co lo trinh."
            : string.Join(" -> ", orderedStops
                .Select(x => x.Poi?.Name ?? $"POI #{x.PoiId}")
                .Take(4));

        if (orderedStops.Count > 4)
            routePreview = $"{routePreview} -> +{orderedStops.Count - 4} stop";

        var firstStop = orderedStops.FirstOrDefault();
        var firstStopName = firstStop?.Poi?.Name
                            ?? (firstStop is not null
                                ? $"POI #{firstStop.PoiId}"
                                : "Chua co stop");

        var startButton = new Button
        {
            Text = "Bat dau tour nay",
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
                        Text = string.IsNullOrWhiteSpace(tour.Description)
                            ? "Khong co mo ta."
                            : tour.Description,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#617A74")
                    },
                    new Label
                    {
                        Text = $"So stop: {orderedStops.Count} | Stop dau: {firstStopName}",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1F6F64")
                    },
                    new Label
                    {
                        Text = $"Lo trinh: {routePreview}",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#48635F")
                    },
                    startButton
                }
            }
        };
    }

    private async Task StartTourAsync(Tour tour)
    {
        var currentSession = _tourSessionService.GetCurrentSession();
        if (currentSession is not null &&
            !currentSession.IsFinished &&
            currentSession.TourId != tour.Id)
        {
            var replace = await DisplayAlertAsync(
                "Doi tour",
                $"Tour \"{currentSession.TourName}\" dang chay. Ban co muon doi sang \"{tour.Name}\" khong?",
                "Dong y",
                "Huy");

            if (!replace)
                return;
        }

        _tourSessionService.Start(tour);
        RefreshActiveSessionCard();
        await Navigation.PushAsync(_serviceProvider.GetRequiredService<TourSessionPage>());
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
            ? "Tour nay da hoan thanh."
            : $"Dang dan den: {session.CurrentStop?.Poi?.Name ?? $"POI #{session.CurrentStop?.PoiId}"}";
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadToursAsync();
    }

    private async void OnOpenCurrentTourClicked(object? sender, EventArgs e)
    {
        if (_tourSessionService.GetCurrentSession() is null)
            return;

        await Navigation.PushAsync(_serviceProvider.GetRequiredService<TourSessionPage>());
    }

    private async void OnCancelCurrentTourClicked(object? sender, EventArgs e)
    {
        if (_tourSessionService.GetCurrentSession() is null)
            return;

        var confirmed = await DisplayAlertAsync(
            "Ket thuc tour",
            "Ban co chac muon dong tour hien tai khong?",
            "Dong y",
            "Huy");

        if (!confirmed)
            return;

        _tourSessionService.Cancel();
        RefreshActiveSessionCard();
    }
}
