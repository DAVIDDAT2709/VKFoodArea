namespace VKFoodArea.Web.Dtos;

public class ResolveQrResponseDto
{
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string MatchedCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public PoiDto? Poi { get; set; }
    public TourDto? Tour { get; set; }
}
