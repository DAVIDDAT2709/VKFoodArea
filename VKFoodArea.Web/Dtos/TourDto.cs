namespace VKFoodArea.Web.Dtos;

public class TourDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<TourStopDto> Stops { get; set; } = new();
}

public class TourStopDto
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int DisplayOrder { get; set; }
    public string Note { get; set; } = string.Empty;
    public PoiDto Poi { get; set; } = new();
}
