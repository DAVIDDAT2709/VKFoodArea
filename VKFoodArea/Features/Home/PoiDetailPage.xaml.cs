using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class PoiDetailPage : ContentPage
{
    private readonly NarrationService _narrationService;
    private readonly FoodRepository _foodRepository;

    public Poi Poi { get; }

    public PoiDetailPage(
        Poi poi,
        NarrationService narrationService,
        FoodRepository foodRepository)
    {
        InitializeComponent();

        Poi = poi;
        _narrationService = narrationService;
        _foodRepository = foodRepository;

        BindingContext = this;
    }

    private async void OnPlayNarrationClicked(object sender, EventArgs e)
{
    try
    {
        await _narrationService.PlayPoiAsync(Poi);
    }
    catch
    {
        await DisplayAlert("Thông báo", "Không thể phát thuyết minh lúc này.", "OK");
    }
}
private async void OnStopNarrationClicked(object sender, EventArgs e)
{
    await _narrationService.StopAsync();
}
    private async void OnBookClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new BookingPage(Poi, _foodRepository));
    }
}
