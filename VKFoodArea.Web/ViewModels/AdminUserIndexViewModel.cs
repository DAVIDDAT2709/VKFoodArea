namespace VKFoodArea.Web.ViewModels;

public class AdminUserIndexViewModel
{
    public PagedListViewModel<AdminUserListItemViewModel> Items { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int LockedCount { get; set; }
    public int AdminCount { get; set; }
    public int OwnerCount { get; set; }
    public List<AuditLogListItemViewModel> RecentAuditLogs { get; set; } = new();
}

public class AdminUserListItemViewModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int OwnedPoiCount { get; set; }
}

public class AuditLogListItemViewModel
{
    public int Id { get; set; }
    public string ActorUsername { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}