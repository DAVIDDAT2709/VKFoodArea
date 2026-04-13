namespace VKFoodArea.Models;

public class Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<TourStop> Stops { get; set; } = new();
}
