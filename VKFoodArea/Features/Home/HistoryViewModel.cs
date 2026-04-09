using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public class HistoryViewModel : INotifyPropertyChanged
{
    private readonly HistoryService _historyService;
    private readonly NarrationService _narrationService;
    private readonly AuthService _authService;
    private readonly AppTextService _text;
    private HistoryItemViewModel? _selectedItem;
    private string _syncStatusText = string.Empty;
    private string _selectedDetailTitle = string.Empty;
    private string _selectedDetailMeta = string.Empty;
    private string _selectedDetailStatus = string.Empty;
    private bool _isDetailVisible;
    private bool _canReplaySelected;

    public HistoryViewModel(
        HistoryService historyService,
        NarrationService narrationService,
        AuthService authService,
        AppTextService text)
    {
        _historyService = historyService;
        _narrationService = narrationService;
        _authService = authService;
        _text = text;
    }

    public ObservableCollection<HistoryItemViewModel> Items { get; } = new();

    public HistoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

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

    public string SelectedDetailTitle
    {
        get => _selectedDetailTitle;
        private set
        {
            _selectedDetailTitle = value;
            OnPropertyChanged();
        }
    }

    public string SelectedDetailMeta
    {
        get => _selectedDetailMeta;
        private set
        {
            _selectedDetailMeta = value;
            OnPropertyChanged();
        }
    }

    public string SelectedDetailStatus
    {
        get => _selectedDetailStatus;
        private set
        {
            _selectedDetailStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsDetailVisible
    {
        get => _isDetailVisible;
        private set
        {
            _isDetailVisible = value;
            OnPropertyChanged();
        }
    }

    public bool CanReplaySelected
    {
        get => _canReplaySelected;
        private set
        {
            _canReplaySelected = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadListeningHistoryAsync()
    {
        var selectedHistoryId = SelectedItem?.Id;
        var result = await _historyService.GetListeningHistoryAsync(_authService.GetCurrentUserId());

        Items.Clear();

        foreach (var record in result.Records)
        {
            Items.Add(new HistoryItemViewModel
            {
                Id = record.Id,
                PoiId = record.PoiId,
                PoiName = record.PoiName,
                PlayedAtUtc = record.PlayedAtUtc,
                PlayedAtText = record.PlayedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                MetaText = $"{_text.GetModeDisplay(record.Mode)} • {_text.GetLanguageDisplay(record.Language)} • {GetOriginDisplay(record.Origin)}",
                CanReplay = record.CanReplay
            });
        }

        SyncStatusText = result.HasRemoteData
            ? BuildLiveStatusText(result.RemoteCount)
            : BuildLocalOnlyStatusText(result.LocalCount);

        OnPropertyChanged(nameof(SummaryText));

        if (selectedHistoryId.HasValue)
        {
            var selected = Items.FirstOrDefault(x => x.Id == selectedHistoryId.Value);
            if (selected is not null)
                await SelectPlaybackHistoryAsync(selected);
            else
                ClearSelectedDetail();
        }
        else
        {
            ClearSelectedDetail();
        }
    }

    public async Task SelectPlaybackHistoryAsync(HistoryItemViewModel? item)
    {
        SelectedItem = item;

        if (item is null)
        {
            ClearSelectedDetail();
            return;
        }

        var detail = await _historyService.GetHistoryDetailAsync(item.Id);
        if (detail is null)
        {
            SelectedDetailTitle = item.PoiName;
            SelectedDetailMeta = item.PlayedAtText;
            SelectedDetailStatus = GetDetailUnavailableText();
            CanReplaySelected = false;
            IsDetailVisible = true;
            return;
        }

        SelectedDetailTitle = detail.PoiName;
        SelectedDetailMeta =
            $"{detail.PlayedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} • " +
            $"{_text.GetModeDisplay(detail.Mode)} • {_text.GetLanguageDisplay(detail.Language)} • {GetOriginDisplay(detail.Origin)}";
        SelectedDetailStatus = detail.CanReplay
            ? GetDetailReadyText()
            : GetDetailUnavailableText();
        CanReplaySelected = detail.CanReplay;
        IsDetailVisible = true;
    }

    public async Task<HistoryReplayResult> ReplaySelectedHistoryAsync()
    {
        if (SelectedItem is null)
            return HistoryReplayResult.Fail(GetDetailUnavailableText());

        var source = await _historyService.GetPlaybackSourceAsync(SelectedItem.Id);
        if (source is null)
        {
            SelectedDetailStatus = GetDetailUnavailableText();
            CanReplaySelected = false;
            return HistoryReplayResult.Fail(SelectedDetailStatus);
        }

        SelectedDetailStatus = BuildReplayStatusText(source.PoiName);
        CanReplaySelected = true;

        await _narrationService.PlayPoiAsync(
            source.PoiId,
            "history",
            source.Language,
            source.Mode);

        SelectedDetailStatus = BuildReplayCompleteText(source.PoiName);
        return HistoryReplayResult.Success();
    }

    public async Task ClearHistoryAsync()
    {
        await _historyService.ClearLocalHistoryAsync();
        SelectedItem = null;
        ClearSelectedDetail();
        await LoadListeningHistoryAsync();
    }

    private void ClearSelectedDetail()
    {
        SelectedDetailTitle = string.Empty;
        SelectedDetailMeta = string.Empty;
        SelectedDetailStatus = string.Empty;
        CanReplaySelected = false;
        IsDetailVisible = false;
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

    private string GetDetailReadyText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "Replay source is ready.",
            "zh" => "已准备好重播该讲解。",
            "ja" => "この案内は再生できます。",
            "de" => "Die Wiedergabe ist bereit.",
            _ => "Bản ghi này đã sẵn sàng để nghe lại."
        };
    }

    private string GetDetailUnavailableText()
    {
        return _text.CurrentLanguage switch
        {
            "en" => "Replay source is unavailable for this record.",
            "zh" => "当前记录没有可重播的来源。",
            "ja" => "この履歴には再生元がありません。",
            "de" => "Für diesen Eintrag ist keine Wiedergabequelle verfügbar.",
            _ => "Bản ghi này hiện chưa có nguồn để nghe lại."
        };
    }

    private string BuildReplayStatusText(string poiName)
    {
        return _text.CurrentLanguage switch
        {
            "en" => $"Replaying: {poiName}",
            "zh" => $"正在重播：{poiName}",
            "ja" => $"再生中: {poiName}",
            "de" => $"Wiedergabe: {poiName}",
            _ => $"Đang nghe lại: {poiName}"
        };
    }

    private string BuildReplayCompleteText(string poiName)
    {
        return _text.CurrentLanguage switch
        {
            "en" => $"Playing again: {poiName}",
            "zh" => $"已再次播放：{poiName}",
            "ja" => $"再度再生中: {poiName}",
            "de" => $"Erneut abgespielt: {poiName}",
            _ => $"Đã phát lại: {poiName}"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class HistoryItemViewModel
{
    public int Id { get; set; }
    public int? PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public DateTime PlayedAtUtc { get; set; }
    public string PlayedAtText { get; set; } = string.Empty;
    public string MetaText { get; set; } = string.Empty;
    public bool CanReplay { get; set; }
}

public sealed record HistoryReplayResult(bool IsSuccess, string? Message = null)
{
    public static HistoryReplayResult Success() => new(true, null);

    public static HistoryReplayResult Fail(string message) => new(false, message);
}
