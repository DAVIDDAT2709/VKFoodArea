using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using VKFoodArea.Data;

namespace VKFoodArea.Features.Home;

public partial class HistoryPage : ContentPage
{
    private readonly AppDbContext _db;

    public ObservableCollection<HistoryItemViewModel> Items { get; } = new();

    public string SummaryText =>
        Items.Count == 0
            ? "Chưa có lượt nghe nào."
            : $"{Items.Count} lượt nghe gần nhất";

    public HistoryPage(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi lịch sử nghe", ex.Message, "OK");
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
            await DisplayAlert("Lỗi lịch sử nghe", ex.Message, "OK");
        }
    }

    private async void OnClearClicked(object sender, EventArgs e)
    {
        if (Items.Count == 0)
        {
            await DisplayAlert("Thông báo", "Hiện chưa có lịch sử để xóa.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Xóa lịch sử",
            "Bạn có chắc muốn xóa toàn bộ lịch sử nghe không?",
            "Xóa",
            "Hủy");

        if (!confirm)
            return;

        var logs = await _db.NarrationLogs.ToListAsync();
        _db.NarrationLogs.RemoveRange(logs);
        await _db.SaveChangesAsync();

        await LoadAsync();
    }

    private static (string ModeText, string LanguageText) ParseMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("TTS", "Tiếng Việt");

        var parts = raw.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var mode = parts.Length > 0 ? parts[0] : "TTS";
        var language = parts.Length > 1 ? parts[1] : "vi";

        var modeText = mode switch
        {
            "Auto" => "Tự động",
            "Audio" => "Audio",
            "TTS" => "TTS",
            _ => mode
        };

        var languageText = language switch
        {
            "en" => "English",
            "zh" => "中文",
            "ja" => "日本語",
            "de" => "Deutsch",
            _ => "Tiếng Việt"
        };

        return (modeText, languageText);
    }
}

public sealed class HistoryItemViewModel
{
    public string PoiName { get; set; } = string.Empty;
    public string PlayedAtText { get; set; } = string.Empty;
    public string MetaText { get; set; } = string.Empty;
}