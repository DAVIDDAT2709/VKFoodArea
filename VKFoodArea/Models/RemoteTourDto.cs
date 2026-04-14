namespace VKFoodArea.Models;

public class RemoteTourDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TtsScriptVi { get; set; } = string.Empty;
    public string TtsScriptEn { get; set; } = string.Empty;
    public string TtsScriptZh { get; set; } = string.Empty;
    public string TtsScriptJa { get; set; } = string.Empty;
    public string TtsScriptDe { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<RemoteTourStopDto> Stops { get; set; } = new();
}

public class RemoteTourStopDto
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int DisplayOrder { get; set; }
    public string Note { get; set; } = string.Empty;
    public RemotePoiDto Poi { get; set; } = new();
}
