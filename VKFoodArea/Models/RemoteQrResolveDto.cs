namespace VKFoodArea.Models;

public class RemoteQrResolveDto
{
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string MatchedCode { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public RemotePoiDto? Poi { get; set; }
    public RemoteTourDto? Tour { get; set; }
}
