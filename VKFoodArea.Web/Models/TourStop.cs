using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class TourStop
{
    public int Id { get; set; }

    public int TourId { get; set; }
    public Tour? Tour { get; set; }

    public int PoiId { get; set; }
    public Poi? Poi { get; set; }

    public int DisplayOrder { get; set; }

    [StringLength(300)]
    public string Note { get; set; } = string.Empty;
}
