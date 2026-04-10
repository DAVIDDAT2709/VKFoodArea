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
        var result = await _historyService.GetListeningHistoryAsync(
            _authService.GetCurrentUserId(),
            _authService.GetCurrentUserSyncKey());

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
                MetaText = $"{_text.GetModeDisplay(record.Mode)} | {_text.GetLanguageDisplay(record.Language)} | {GetOriginDisplay(record.Origin)}",
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

        var detail = await _historyService.GetHistoryDetailAsync(item.Id, _authService.GetCurrentUserId());
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
            $"{detail.PlayedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} | " +
            $"{_text.GetModeDisplay(detail.Mode)} | {_text.GetLanguageDisplay(detail.Language)} | {GetOriginDisplay(detail.Origin)}";
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

        var source = await _historyService.GetPlaybackSourceAsync(
            SelectedItem.Id,
            _authService.GetCurrentUserId());
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
        await _historyService.ClearHistoryAsync(
            _authService.GetCurrentUserId(),
            _authService.GetCurrentUserSyncKey());
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
        => _text.Format("History.LiveStatus", remoteCount);

    private string BuildLocalOnlyStatusText(int localCount)
        => _text.Format("History.LocalStatus", localCount);

    private string GetOriginDisplay(string origin)
    {
        var normalized = (origin ?? string.Empty).Trim().ToLowerInvariant();

        return normalized == "web"
            ? _text["History.OriginWeb"]
            : _text["History.OriginApp"];
    }

    private string GetDetailReadyText() => _text["History.DetailReady"];

    private string GetDetailUnavailableText() => _text["History.DetailUnavailable"];

    private string BuildReplayStatusText(string poiName)
        => _text.Format("History.ReplayStatus", poiName);

    private string BuildReplayCompleteText(string poiName)
        => _text.Format("History.ReplayComplete", poiName);

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
