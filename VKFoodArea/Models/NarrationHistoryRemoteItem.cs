namespace VKFoodArea.Models;

public sealed class NarrationHistoryRemoteItem
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string Language { get; set; } = "vi";
    public string TriggerSource { get; set; } = "manual";
    public string Mode { get; set; } = "tts";
    public DateTime PlayedAt { get; set; }
}
