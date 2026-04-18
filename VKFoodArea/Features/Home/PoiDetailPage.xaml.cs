using VKFoodArea.Models;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class PoiDetailPage : ContentPage
{
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly int? _tourId;
    private readonly string _tourName;

    public Poi Poi { get; }

    public PoiDetailPage(
        Poi poi,
        NarrationService narrationService,
        AppTextService text,
        NarrationUiStateService narrationUiState,
        int? tourId = null,
        string? tourName = null)
    {
        InitializeComponent();

        Poi = poi;
        _narrationService = narrationService;
        _text = text;
        _narrationUiState = narrationUiState;
        _tourId = tourId.HasValue && tourId.Value > 0 ? tourId.Value : null;
        _tourName = (tourName ?? string.Empty).Trim();

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();
    }

    private async void OnPlayNarrationClicked(object sender, EventArgs e)
    {
        try
        {
            if (_narrationUiState.IsPlaying && _narrationUiState.PoiId == Poi.Id)
            {
                await _narrationService.StopAsync();
                return;
            }

            _narrationUiState.SetContext(Poi);
            await _narrationService.PlayPoiAsync(
                Poi.Id,
                _tourId.HasValue ? "tour" : "manual",
                tourId: _tourId,
                tourName: _tourName);
        }
        catch
        {
            await DisplayAlertAsync(_text["PoiDetail.PlayErrorTitle"], _text["PoiDetail.PlayErrorMessage"], _text["Common.Ok"]);
        }
    }

    private async void OnStopNarrationClicked(object sender, EventArgs e)
    {
        await _narrationService.StopAsync();
    }

    private void ApplyLocalizedText()
    {
        Title = Poi.Name;
        BadgeLabel.Text = _text["PoiDetail.Badge"];
        AudioGuideLabel.Text = _text["PoiDetail.AudioGuide"];
        PlayCardLabel.Text = _text["PoiDetail.Play"];
        DetailTitleLabel.Text = _text["PoiDetail.DetailTitle"];
        DescriptionSectionLabel.Text = _text["PoiDetail.DescriptionSection"];
        PlayNarrationButton.Text = $"▶ {_text["PoiDetail.Play"]}";
        StopNarrationButton.Text = $"⏹ {_text["PoiDetail.Stop"]}";
    }
}
