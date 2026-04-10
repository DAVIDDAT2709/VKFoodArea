using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Data;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;
    private bool _isRefreshing;
    private bool _autoRefreshStarted;
    private bool _autoRefreshEnabled;

    public HistoryPage(
        HistoryViewModel viewModel,
        AppRootNavigationService rootNavigationService,
        IServiceProvider serviceProvider,
        AppTextService text)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _rootNavigationService = rootNavigationService;
        _serviceProvider = serviceProvider;
        _text = text;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedTextClean();
        StartAutoRefresh();

        try
        {
            await _viewModel.LoadListeningHistoryAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_text["History.LoadErrorTitle"], ex.Message, _text["Common.Ok"]);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _autoRefreshEnabled = false;
    }

    private async Task RefreshHistoryAsync(bool showLoadError = false)
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            await _viewModel.LoadListeningHistoryAsync();
        }
        catch when (!showLoadError)
        {
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void StartAutoRefresh()
    {
        _autoRefreshEnabled = true;

        if (_autoRefreshStarted || Dispatcher is null)
            return;

        _autoRefreshStarted = true;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(5), () =>
        {
            if (!_autoRefreshEnabled)
            {
                _autoRefreshStarted = false;
                return false;
            }

            if (!_isRefreshing)
                _ = RefreshHistoryAsync();

            return true;
        });
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            await RefreshHistoryAsync(showLoadError: true);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(_text["History.LoadErrorTitle"], ex.Message, _text["Common.Ok"]);
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        if (_viewModel.Items.Count == 0)
        {
            await DisplayAlertAsync(_text["Common.Error"], _text["History.ClearEmptyMessage"], _text["Common.Ok"]);
            return;
        }

        var confirm = await DisplayAlertAsync(
            _text["History.ClearTitle"],
            _text["History.ClearMessage"],
            _text["Common.Delete"],
            _text["Common.Cancel"]);

        if (!confirm)
            return;

        await _viewModel.ClearHistoryAsync();
    }

    private async void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var item = e.CurrentSelection.FirstOrDefault() as HistoryItemViewModel;
        await _viewModel.SelectPlaybackHistoryAsync(item);
    }

    private async void OnReplayClicked(object sender, EventArgs e)
    {
        var result = await _viewModel.ReplaySelectedHistoryAsync();

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
            await DisplayAlertAsync(_text["Common.Error"], result.Message, _text["Common.Ok"]);
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.FirstOrDefault() is HomeDesignPage)
        {
            await Navigation.PopToRootAsync();
            return;
        }

        await _rootNavigationService.SetRootAsync<HomeDesignPage>();
    }

    private async void OnOpenFullMapClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<FullMapPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnHistoryClickedCurrent(object sender, EventArgs e)
    {
        await Task.CompletedTask;
    }

    private async void OnUserClicked(object sender, EventArgs e)
    {
        var page = _serviceProvider.GetRequiredService<User.UserPage>();
        await Navigation.PushAsync(page);
    }

    private void ApplyLocalizedText()
    {
        Title = _text["History.PageTitle"];
        HeaderTitleLabel.Text = _text["History.PageTitle"];
        RefreshButton.Text = _text["Common.Refresh"];
        ClearButton.Text = _text["Common.Delete"];
        DetailTitleLabel.Text = _viewModel.SelectedDetailTitle;
        ReplayButton.Text = GetReplayButtonText();
        EmptyTitleLabel.Text = _text["History.EmptyTitle"];
        EmptyMessageLabel.Text = _text["History.EmptyMessage"];
        NavHomeButton.Text = $"🏠\n{_text["Nav.Home"]}";
        NavMapButton.Text = $"🗺\n{_text["Nav.Map"]}";
        NavHistoryButton.Text = $"🕘\n{_text["Nav.History"]}";
        NavAccountButton.Text = $"👤\n{_text["Nav.Account"]}";
    }

    private void ApplyLocalizedTextClean()
    {
        ApplyLocalizedText();
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }

    private string GetReplayButtonText()
        => _text["History.ReplayButton"];
}
