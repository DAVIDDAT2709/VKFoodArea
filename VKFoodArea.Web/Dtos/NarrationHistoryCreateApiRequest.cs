namespace VKFoodArea.Web.Dtos;

public class NarrationHistoryCreateApiRequest
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string TriggerSource { get; set; } = "manual";
    public string Mode { get; set; } = "tts";
    public DateTime? PlayedAt { get; set; }
}