using VKFoodArea.Models;

namespace VKFoodArea.Features.Home;

public sealed class HomePoiSuggestion
{
    public required Poi Poi { get; init; }

    public string Name => Poi.Name;
    public string Address => Poi.Address;
}

public sealed class FeaturedFoodCardViewModel
{
    public required FoodItem Food { get; init; }
    public required string PriceText { get; init; }

    public string Name => Food.Name;
    public string RestaurantName => Food.RestaurantName;
    public string ImageUrl => Food.ImageUrl;
}
