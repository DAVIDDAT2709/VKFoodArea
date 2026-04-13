namespace VKFoodArea.Models;

public class TourStop
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int DisplayOrder { get; set; }
    public string Note { get; set; } = string.Empty;
    public Poi Poi { get; set; } = new();
}
