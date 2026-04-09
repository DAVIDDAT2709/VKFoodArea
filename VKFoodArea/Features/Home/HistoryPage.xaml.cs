using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Data;
using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class HistoryPage : ContentPage
{
    private readonly AppDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppTextService _text;
    private readonly NarrationSyncService _narrationSyncService;
    private bool _isRefreshing;
    private bool _autoRefreshStarted;
    private bool _autoRefreshEnabled;
    private string _syncStatusText = string.Empty;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = new();

    public string SummaryText =>
        Items.Count == 0
            ? _text["History.NoneSummary"]
            : _text.Format("History.CountSummary", Items.Count);

    public string SyncStatusText
    {
        get => _syncStatusText;
        private set
        {
            _syncStatusText = value;
            OnPropertyChanged();
        }
    }

    public HistoryPage(
        AppDbContext db,
        IServiceProvider serviceProvider,
        AppTextService text,
        NarrationSyncService narrationSyncService)
    {
        InitializeComponent();
        _db = db;
        _serviceProvider = serviceProvider;
        _text = text;
        _narrationSyncService = narrationSyncService;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedTextClean();
        StartAutoRefresh();

        try
        {
            await LoadAsync(showLoadError: true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(_text["History.LoadErrorTitle"], ex.Message, _text["Common.Ok"]);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _autoRefreshEnabled = false;
    }

    private async Task LoadAsync(bool showLoadError = false)
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;

        try
        {
            var localRows = await LoadLocalRowsAsync();
            List<HistoryRow>? remoteRows = null;

            try
            {
                var remoteItems = await _narrationSyncService.GetRecentHistoryAsync(top: 100);
                remoteRows = remoteItems
                    .Select(MapRemoteRow)
                    .ToList();

                SyncStatusText = BuildLiveStatusText(remoteRows.Count);
            }
            catch
            {
                SyncStatusText = BuildLocalOnlyStatusText(localRows.Count);
            }

            var mergedRows = MergeRows(localRows, remoteRows)
                .OrderByDescending(x => x.PlayedAtUtc)
                .Take(100)
                .ToList();

            Items.Clear();

            foreach (var row in mergedRows)
            {
                Items.Add(new HistoryItemViewModel
                {
                    PoiName = row.PoiName,
                    PlayedAtText = row.PlayedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    MetaText = $"{_text.GetModeDisplay(row.Mode)} • {_text.GetLanguageDisplay(row.Language)} • {GetOriginDisplay(row.Origin)}"
                });
            }

            OnPropertyChanged(nameof(SummaryText));
        }
        catch when (!showLoadError)
        {
            SyncStatusText = BuildLocalOnlyStatusText(Items.Count);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task<List<HistoryRow>> LoadLocalRowsAsync()
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

        return rawRows
            .OrderByDescending(x => x.PlayedAt)
            .Select(x =>
            {
                var (mode, language) = ParseModeValues(x.Mode);

                return new HistoryRow
                {
                    PoiName = x.PoiName,
                    PlayedAtUtc = x.PlayedAt.UtcDateTime,
                    Mode = mode,
                    Language = language,
                    Origin = "app"
                };
            })
            .ToList();
    }

    private static HistoryRow MapRemoteRow(NarrationHistoryRemoteItem item)
    {
        return new HistoryRow
        {
            PoiName = item.PoiName,
            PlayedAtUtc = DateTime.SpecifyKind(item.PlayedAt, DateTimeKind.Utc),
            Mode = item.Mode,
            Language = item.Language,
            Origin = "web"
        };
    }

    private List<HistoryRow> MergeRows(List<HistoryRow> localRows, List<HistoryRow>? remoteRows)
    {
        if (remoteRows is null || remoteRows.Count == 0)
            return localRows;

        var merged = new List<HistoryRow>(remoteRows);

        foreach (var localRow in localRows)
        {
            var hasRemoteDuplicate = remoteRows.Any(remoteRow =>
                string.Equals(remoteRow.PoiName, localRow.PoiName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(remoteRow.Mode, localRow.Mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(remoteRow.Language, localRow.Language, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((remoteRow.PlayedAtUtc - localRow.PlayedAtUtc).TotalSeconds) <= 3);

            if (!hasRemoteDuplicate)
                merged.Add(localRow);
        }

        return merged;
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
                _ = LoadAsync();

            return true;
        });
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            await LoadAsync(showLoadError: true);
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

        await LoadAsync(showLoadError: true);
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

        if (string.IsNullOrWhiteSpace(SyncStatusText))
            SyncStatusText = BuildLocalOnlyStatusText(Items.Count);

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

    private (string Mode, string Language) ParseModeValues(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("tts", "vi");

        var parts = raw.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var mode = parts.Length > 0 ? parts[0].Trim().ToLowerInvariant() : "tts";
        var language = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "vi";

        return (mode, language);
    }

    private string BuildLiveStatusText(int remoteCount)
    {
        return _text.CurrentLanguage switch
        {
            "en" => $"Realtime app/web sync • {remoteCount} web items",
            "zh" => $"应用与网页实时同步 • 网页 {remoteCount} 条",
            "ja" => $"アプリとWebを自動同期中 • Web {remoteCount} 件",
            "de" => $"App/Web Echtzeitabgleich • {remoteCount} Web-Einträge",
            _ => $"Đồng bộ realtime App/Web • {remoteCount} bản ghi web"
        };
    }

    private string BuildLocalOnlyStatusText(int localCount)
    {
        return _text.CurrentLanguage switch
        {
            "en" => $"Using local app history • {localCount} items",
            "zh" => $"当前使用应用本地记录 • {localCount} 条",
            "ja" => $"現在はアプリ内履歴を表示中 • {localCount} 件",
            "de" => $"Lokalen App-Verlauf anzeigen • {localCount} Einträge",
            _ => $"Đang dùng lịch sử local trong app • {localCount} bản ghi"
        };
    }

    private string GetOriginDisplay(string origin)
    {
        var normalized = (origin ?? string.Empty).Trim().ToLowerInvariant();

        return _text.CurrentLanguage switch
        {
            "en" => normalized == "web" ? "Web" : "App",
            "zh" => normalized == "web" ? "网页" : "应用",
            "ja" => normalized == "web" ? "Web" : "アプリ",
            "de" => normalized == "web" ? "Web" : "App",
            _ => normalized == "web" ? "Web" : "App"
        };
    }

    private sealed class HistoryRow
    {
        public string PoiName { get; init; } = string.Empty;
        public DateTime PlayedAtUtc { get; init; }
        public string Mode { get; init; } = "tts";
        public string Language { get; init; } = "vi";
        public string Origin { get; init; } = "app";
    }
}

public sealed class HistoryItemViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public string PlayedAtText { get; set; } = string.Empty;
    public string MetaText { get; set; } = string.Empty;
}
