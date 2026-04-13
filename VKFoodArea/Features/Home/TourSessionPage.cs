using Microsoft.Maui.Controls.Shapes;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class TourSessionPage : ContentPage
{
    private readonly TourSessionService _tourSessionService;
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;

    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _statusLabel;
    private readonly Label _currentStopLabel;
    private readonly Label _currentStopNoteLabel;
    private readonly VerticalStackLayout _completedStopsLayout;
    private readonly Button _openStopButton;
    private readonly Button _endTourButton;

    public TourSessionPage(
        TourSessionService tourSessionService,
        NarrationService narrationService,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        _tourSessionService = tourSessionService;
        _narrationService = narrationService;
        _text = text;
        _narrationUiState = narrationUiState;

        Title = "Tour";

        _titleLabel = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold };
        _descriptionLabel = new Label { FontSize = 14, Opacity = 0.8 };
        _statusLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold };
        _currentStopLabel = new Label { FontSize = 18, FontAttributes = FontAttributes.Bold };
        _currentStopNoteLabel = new Label { FontSize = 14, Opacity = 0.75 };
        _completedStopsLayout = new VerticalStackLayout { Spacing = 8 };

        _openStopButton = new Button
        {
            Text = "Mo stop hien tai",
            HorizontalOptions = LayoutOptions.Fill
        };
        _openStopButton.Clicked += OnOpenCurrentStopClicked;

        _endTourButton = new Button
        {
            Text = "Ket thuc tour",
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent,
            BorderColor = Color.FromArgb("#c0392b"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#c0392b")
        };
        _endTourButton.Clicked += OnEndTourClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20),
                Spacing = 18,
                Children =
                {
                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 18 },
                        BackgroundColor = Color.FromArgb("#f4f9f6"),
                        Padding = new Thickness(18),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                _titleLabel,
                                _descriptionLabel,
                                _statusLabel,
                                _currentStopLabel,
                                _currentStopNoteLabel,
                                _openStopButton,
                                _endTourButton
                            }
                        }
                    },
                    new Label
                    {
                        Text = "Tien do tour",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    },
                    _completedStopsLayout
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _tourSessionService.StateChanged += OnTourSessionChanged;
        RefreshSessionUi();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _tourSessionService.StateChanged -= OnTourSessionChanged;
    }

    private void OnTourSessionChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshSessionUi);
    }

    private async void OnOpenCurrentStopClicked(object? sender, EventArgs e)
    {
        var session = _tourSessionService.GetCurrentSession();
        var currentStop = session?.CurrentStop;
        if (currentStop?.Poi is null)
            return;

        _narrationUiState.SetContext(currentStop.Poi);
        await Navigation.PushAsync(new PoiDetailPage(
            currentStop.Poi,
            _narrationService,
            _text,
            _narrationUiState));
    }

    private async void OnEndTourClicked(object? sender, EventArgs e)
    {
        _tourSessionService.Cancel();
        await Navigation.PopAsync();
    }

    private void RefreshSessionUi()
    {
        var session = _tourSessionService.GetCurrentSession();
        if (session is null)
        {
            _titleLabel.Text = "Khong co tour dang chay";
            _descriptionLabel.Text = "Quet QR tour de bat dau.";
            _statusLabel.Text = string.Empty;
            _currentStopLabel.Text = string.Empty;
            _currentStopNoteLabel.Text = string.Empty;
            _openStopButton.IsVisible = false;
            _completedStopsLayout.Children.Clear();
            return;
        }

        var currentStop = session.CurrentStop;
        _titleLabel.Text = session.TourName;
        _descriptionLabel.Text = session.TourDescription;
        _statusLabel.Text = session.IsFinished
            ? $"Da hoan thanh tour luc {session.StartedAt.LocalDateTime:dd/MM HH:mm}"
            : $"Dang chay tu {session.StartedAt.LocalDateTime:dd/MM HH:mm}";
        _currentStopLabel.Text = currentStop?.Poi?.Name ?? "Da hoan thanh tat ca stop";
        _currentStopNoteLabel.Text = currentStop?.Note ?? string.Empty;
        _openStopButton.IsVisible = currentStop?.Poi is not null;

        _completedStopsLayout.Children.Clear();
        foreach (var stop in session.OrderedStops)
        {
            var isDone = session.CompletedStopIds.Contains(stop.Id);
            var isCurrent = currentStop?.Id == stop.Id;

            _completedStopsLayout.Children.Add(new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                BackgroundColor = isCurrent ? Color.FromArgb("#edf8ef") : Colors.White,
                Stroke = isCurrent ? Color.FromArgb("#16a34a") : Color.FromArgb("#d8e3dd"),
                Padding = new Thickness(14, 10),
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label
                        {
                            Text = $"{stop.DisplayOrder}. {stop.Poi?.Name ?? $"POI #{stop.PoiId}"}",
                            FontAttributes = FontAttributes.Bold
                        },
                        new Label
                        {
                            Text = string.IsNullOrWhiteSpace(stop.Note) ? (isDone ? "Da hoan thanh" : isCurrent ? "Stop hien tai" : "Dang cho") : stop.Note,
                            FontSize = 13,
                            Opacity = 0.8
                        }
                    }
                }
            });
        }
    }
}
