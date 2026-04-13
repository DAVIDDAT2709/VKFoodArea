namespace VKFoodArea.Web.ViewModels;

public class QrCodeItemListItemViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public bool IsTargetActive { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
