using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class TourSessionPage : ContentPage
{
    private readonly TourSessionService _tourSessionService;
    private readonly NarrationService _narrationService;
    private readonly TourNarrationService _tourNarrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;

    private readonly Label _titleLabel;
    private readonly Label _descriptionLabel;
    private readonly Label _statusLabel;
    private readonly Label _currentStopLabel;
    private readonly Label _currentStopNoteLabel;
    private readonly Label _progressTitleLabel;
    private readonly VerticalStackLayout _completedStopsLayout;
    private readonly Button _openStopButton;
    private readonly Button _endTourButton;

    public TourSessionPage(
        TourSessionService tourSessionService,
        NarrationService narrationService,
        TourNarrationService tourNarrationService,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        _tourSessionService = tourSessionService;
        _narrationService = narrationService;
        _tourNarrationService = tourNarrationService;
        _text = text;
        _narrationUiState = narrationUiState;
        BackgroundColor = Color.FromArgb("#EEF4F1");

        _titleLabel = new Label
        {
            FontSize = 27,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "OpenSansSemibold",
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _descriptionLabel = new Label
        {
            FontSize = 14,
            TextColor = Color.FromArgb("#E8FFFC"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        _statusLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#DDF7F1")
        };
        _currentStopLabel = new Label
        {
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "OpenSansSemibold",
            TextColor = Color.FromArgb("#173330"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        _currentStopNoteLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#617A74"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        _progressTitleLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "OpenSansSemibold",
            TextColor = Color.FromArgb("#173330")
        };
        _completedStopsLayout = new VerticalStackLayout { Spacing = 8 };

        _openStopButton = new Button
        {
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Color.FromArgb("#129488"),
            TextColor = Colors.White,
            CornerRadius = 16,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        _openStopButton.Clicked += OnOpenCurrentStopClicked;

        _endTourButton = new Button
        {
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Color.FromArgb("#FFF8E8"),
            BorderColor = Color.FromArgb("#E7B8AE"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#B8452E"),
            CornerRadius = 16,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        _endTourButton.Clicked += OnEndTourClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 14,
                Children =
                {
                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        BackgroundColor = Color.FromArgb("#0E9A91"),
                        StrokeThickness = 0,
                        Padding = new Thickness(18, 18, 18, 20),
                        Shadow = new Shadow
                        {
                            Brush = new SolidColorBrush(Color.FromArgb("#0E625C")),
                            Offset = new Point(0, 10),
                            Radius = 22,
                            Opacity = 0.08f
                        },
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                _titleLabel,
                                _descriptionLabel,
                                _statusLabel
                            }
                        }
                    },
                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 12 },
                        BackgroundColor = Colors.White,
                        Stroke = Color.FromArgb("#D8E2DE"),
                        StrokeThickness = 1,
                        Padding = new Thickness(16),
                        Shadow = new Shadow
                        {
                            Brush = new SolidColorBrush(Color.FromArgb("#8FAAA3")),
                            Offset = new Point(0, 10),
                            Radius = 18,
                            Opacity = 0.06f
                        },
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Điểm hiện tại",
                                    FontSize = 12,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#129488")
                                },
                                _currentStopLabel,
                                _currentStopNoteLabel,
                                _openStopButton,
                                _endTourButton
                            }
                        }
                    },
                    _progressTitleLabel,
                    _completedStopsLayout
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _tourSessionService.StateChanged += OnTourSessionChanged;
        ApplyLocalizedText();
        RefreshSessionUi();
        _ = TryPlayIntroAsync();
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
        if (session is null || currentStop?.Poi is null)
            return;

        _narrationUiState.SetContext(currentStop.Poi);
        await Navigation.PushAsync(new PoiDetailPage(
            currentStop.Poi,
            _narrationService,
            _text,
            _narrationUiState,
            session.TourId,
            session.TourName));
    }

    private async void OnEndTourClicked(object? sender, EventArgs e)
    {
        _tourSessionService.Cancel();
        await Navigation.PopAsync();
    }

    private async Task TryPlayIntroAsync()
    {
        var session = _tourSessionService.GetCurrentSession();
        var currentLanguage = _tourNarrationService.CurrentLanguage;

        if (session is null ||
            (session.IntroPlayedAt.HasValue &&
             string.Equals(
                 session.IntroPlayedLanguage,
                 currentLanguage,
                 StringComparison.OrdinalIgnoreCase)))
            return;

        _tourSessionService.MarkIntroPlayed(currentLanguage);

        try
        {
            await _tourNarrationService.PlayIntroAsync(session);
        }
        catch
        {
        }
    }

    private void ApplyLocalizedText()
    {
        Title = _text["Tour.PageTitle"];
        _openStopButton.Text = _text["Tour.OpenCurrentStop"];
        _endTourButton.Text = _text["Tour.EndTour"];
        _progressTitleLabel.Text = _text["Tour.SessionProgressTitle"];
    }

    private void RefreshSessionUi()
    {
        var session = _tourSessionService.GetCurrentSession();
        if (session is null)
        {
            _titleLabel.Text = _text["Tour.SessionEmptyTitle"];
            _descriptionLabel.Text = _text["Tour.SessionEmptySubtitle"];
            _statusLabel.Text = string.Empty;
            _currentStopLabel.Text = string.Empty;
            _currentStopNoteLabel.Text = string.Empty;
            _openStopButton.IsVisible = false;
            _endTourButton.IsVisible = false;
            _completedStopsLayout.Children.Clear();
            return;
        }

        var currentStop = session.CurrentStop;
        _titleLabel.Text = session.TourName;
        _descriptionLabel.Text = _tourNarrationService.ResolveDisplaySummary(session);
        _statusLabel.Text = session.IsFinished
            ? _text.Format("Tour.CompletedAt", session.StartedAt.LocalDateTime)
            : _text.Format("Tour.StartedAt", session.StartedAt.LocalDateTime);
        _currentStopLabel.Text = currentStop?.Poi?.Name ?? _text["Tour.AllStopsCompleted"];
        _currentStopNoteLabel.Text = currentStop?.Note
            ?? (currentStop?.Poi is not null ? "Mở điểm này để xem chi tiết và nghe thuyết minh." : string.Empty);
        _openStopButton.IsVisible = currentStop?.Poi is not null;
        _endTourButton.IsVisible = true;

        _completedStopsLayout.Children.Clear();
        foreach (var stop in session.OrderedStops)
        {
            var isDone = session.CompletedStopIds.Contains(stop.Id);
            var isCurrent = currentStop?.Id == stop.Id;
            var statusText = string.IsNullOrWhiteSpace(stop.Note)
                ? isDone
                    ? _text["Tour.StatusCompleted"]
                    : isCurrent
                        ? _text["Tour.StatusCurrentStop"]
                        : _text["Tour.StatusWaiting"]
                : stop.Note;
            var stepBadgeText = isDone ? "✓" : stop.DisplayOrder.ToString();
            var stepBadgeColor = isDone
                ? Color.FromArgb("#129488")
                : isCurrent
                    ? Color.FromArgb("#F1C95D")
                    : Color.FromArgb("#EAF4F1");
            var stepBadgeTextColor = isDone
                ? Colors.White
                : isCurrent
                    ? Color.FromArgb("#173330")
                    : Color.FromArgb("#617A74");

            _completedStopsLayout.Children.Add(new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                BackgroundColor = isCurrent ? Color.FromArgb("#FFF8E8") : Colors.White,
                Stroke = isCurrent ? Color.FromArgb("#F1D187") : Color.FromArgb("#D8E2DE"),
                StrokeThickness = 1,
                Padding = new Thickness(14, 12),
                Content = new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Border
                        {
                            WidthRequest = 36,
                            HeightRequest = 36,
                            BackgroundColor = stepBadgeColor,
                            StrokeThickness = 0,
                            StrokeShape = new RoundRectangle { CornerRadius = 12 },
                            VerticalOptions = LayoutOptions.Start,
                            Content = new Label
                            {
                                Text = stepBadgeText,
                                FontSize = 13,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = stepBadgeTextColor,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 3,
                            HorizontalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new Label
                                {
                                    Text = stop.Poi?.Name ?? $"POI #{stop.PoiId}",
                                    FontSize = 15,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#173330"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                },
                                new Label
                                {
                                    Text = statusText,
                                    FontSize = 12.5,
                                    TextColor = isCurrent ? Color.FromArgb("#8A5A00") : Color.FromArgb("#617A74"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                }
                            }
                        }
                    }
                }
            });
        }
    }
}
