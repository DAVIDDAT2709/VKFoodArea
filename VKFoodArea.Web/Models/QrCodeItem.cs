using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class QrCodeItem
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string TargetType { get; set; } = QrTargetTypes.Poi;

    public int TargetId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
