namespace VKFoodArea.Web.ViewModels;

public class AdminMapViewModel
{
    public List<AdminMapPoiViewModel> ActivePois { get; set; } = new();
}

public class AdminMapPoiViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public int Priority { get; set; }
}
