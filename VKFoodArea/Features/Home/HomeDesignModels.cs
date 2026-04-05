using VKFoodArea.Models;

namespace VKFoodArea.Features.Home;

public sealed class HomePoiSuggestion
{
    public required Poi Poi { get; init; }

    public string Name => Poi.Name;
    public string Address => Poi.Address;
}
