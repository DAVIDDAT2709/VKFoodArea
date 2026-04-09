using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class PoiDetailPage : ContentPage
{
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;

    public Poi Poi { get; }

    public PoiDetailPage(
        Poi poi,
        NarrationService narrationService,
        FoodRepository foodRepository,
        AppTextService text,
        NarrationUiStateService narrationUiState)
    {
        InitializeComponent();

        Poi = poi;
        _narrationService = narrationService;
        _text = text;
        _narrationUiState = narrationUiState;

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
        _narrationUiState.SetContext(Poi);
        await _narrationService.PlayPoiAsync(Poi);
    }
    catch
    {
        await DisplayAlert(_text["PoiDetail.PlayErrorTitle"], _text["PoiDetail.PlayErrorMessage"], _text["Common.Ok"]);
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
