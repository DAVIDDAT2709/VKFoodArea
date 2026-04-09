using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Data;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class HistoryPage : ContentPage
{
    private readonly AppDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = new();

    public string SummaryText =>
        Items.Count == 0
            ? _text["History.NoneSummary"]
            : _text.Format("History.CountSummary", Items.Count);

    public HistoryPage(AppDbContext db, IServiceProvider serviceProvider, AppTextService text)
    {
        InitializeComponent();
        _db = db;
        _serviceProvider = serviceProvider;
        _text = text;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedTextClean();

        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert(_text["History.LoadErrorTitle"], ex.Message, _text["Common.Ok"]);
        }
    }

    private async Task LoadAsync()
    {
        var rawRows = await (
            from log in _db.NarrationLogs.AsNoTracking()
            join poi in _db.Pois.AsNoTracking() on log.PoiId equals poi.Id into poiGroup
            from poi in poiGroup.DefaultIfEmpty()
            select new
            {
                PoiName = poi != null ? poi.Name : $"POI #{log.PoiId}",
                log.PlayedAt,
                log.Mode
            })
            .ToListAsync();

        var rows = rawRows
            .OrderByDescending(x => x.PlayedAt)
            .Take(100)
            .ToList();

        Items.Clear();

        foreach (var row in rows)
        {
            var (modeText, languageText) = ParseMode(row.Mode);

            Items.Add(new HistoryItemViewModel
            {
                PoiName = row.PoiName,
                PlayedAtText = row.PlayedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                MetaText = $"{modeText} • {languageText}"
            });
        }

        OnPropertyChanged(nameof(SummaryText));
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert(_text["History.LoadErrorTitle"], ex.Message, _text["Common.Ok"]);
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        if (Items.Count == 0)
        {
            await DisplayAlert(_text["Common.Error"], _text["History.ClearEmptyMessage"], _text["Common.Ok"]);
            return;
        }

        var confirm = await DisplayAlert(
            _text["History.ClearTitle"],
            _text["History.ClearMessage"],
            _text["Common.Delete"],
            _text["Common.Cancel"]);

        if (!confirm)
            return;

        var logs = await _db.NarrationLogs.ToListAsync();
        _db.NarrationLogs.RemoveRange(logs);
        await _db.SaveChangesAsync();

        await LoadAsync();
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.OfType<HomeDesignPage>().Any())
        {
            await Navigation.PopToRootAsync();
            return;
        }

        Application.Current!.MainPage =
            new NavigationPage(_serviceProvider.GetRequiredService<HomeDesignPage>());
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
        EmptyTitleLabel.Text = _text["History.EmptyTitle"];
        EmptyMessageLabel.Text = _text["History.EmptyMessage"];
        NavHomeButton.Text = $"🏠\n{_text["Nav.Home"]}";
        NavMapButton.Text = $"🗺\n{_text["Nav.Map"]}";
        NavHistoryButton.Text = $"🕘\n{_text["Nav.History"]}";
        NavAccountButton.Text = $"👤\n{_text["Nav.Account"]}";
        OnPropertyChanged(nameof(SummaryText));
    }

    private void ApplyLocalizedTextClean()
    {
        ApplyLocalizedText();
        NavHomeButton.Text = _text["Nav.Home"];
        NavMapButton.Text = _text["Nav.Map"];
        NavHistoryButton.Text = _text["Nav.History"];
        NavAccountButton.Text = _text["Nav.Account"];
    }

    private (string ModeText, string LanguageText) ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("TTS", _text.GetLanguageDisplay("vi"));

        var parts = raw.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var mode = parts.Length > 0 ? parts[0] : "TTS";
        var language = parts.Length > 1 ? parts[1] : "vi";

        var modeText = _text.GetModeDisplay(mode);
        var languageText = _text.GetLanguageDisplay(language);

        return (modeText, languageText);
    }
}

public sealed class HistoryItemViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public string PlayedAtText { get; set; } = string.Empty;
    public string MetaText { get; set; } = string.Empty;
}
