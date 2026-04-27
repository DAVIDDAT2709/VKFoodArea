namespace VKFoodArea.Models;

public class AppSyncOutboxItem
{
    public int Id { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime? DiscardedAt { get; set; }
}
