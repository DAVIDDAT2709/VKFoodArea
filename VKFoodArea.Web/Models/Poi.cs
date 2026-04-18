using System.ComponentModel.DataAnnotations;

namespace VKFoodArea.Web.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public int Priority { get; set; } = 1;

    public string Description { get; set; } = string.Empty;

    public string TtsScriptVi { get; set; } = string.Empty;
    public string TtsScriptEn { get; set; } = string.Empty;
    public string TtsScriptZh { get; set; } = string.Empty;
    public string TtsScriptJa { get; set; } = string.Empty;
    public string TtsScriptDe { get; set; } = string.Empty;

    public string AudioFileVi { get; set; } = string.Empty;
    public string AudioFileEn { get; set; } = string.Empty;
    public string AudioFileJa { get; set; } = string.Empty;

    public string QrCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int? OwnerAdminUserId { get; set; }
    public AdminUser? OwnerAdminUser { get; set; }
    public string ApprovalStatus { get; set; } = PoiApprovalStatus.Approved;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminUserId { get; set; }
    public string ReviewNote { get; set; } = string.Empty;

    public List<NarrationHistory> NarrationHistories { get; set; } = new();
    public List<PoiTranslation> Translations { get; set; } = new();
    public List<PoiAudioAsset> AudioAssets { get; set; } = new();
}
