using VKFoodArea.Helpers;

namespace VKFoodArea.Models;

public class QrResolveResult
{
    public string TargetType { get; set; } = QrTargetTypes.Poi;
    public int TargetId { get; set; }
    public string MatchedCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public Poi? Poi { get; set; }
    public Tour? Tour { get; set; }
}
