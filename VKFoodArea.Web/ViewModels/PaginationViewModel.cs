namespace VKFoodArea.Web.ViewModels;

public class PaginationViewModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = PagedListViewModel<object>.DefaultPageSize;
    public int TotalItems { get; set; }
    public string PageParameterName { get; set; } = "page";

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public int FirstItemNumber => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItemNumber => Math.Min(Page * PageSize, TotalItems);

    public static PaginationViewModel From<T>(PagedListViewModel<T> pagedItems)
        => new()
        {
            Page = pagedItems.Page,
            PageSize = pagedItems.PageSize,
            TotalItems = pagedItems.TotalItems
        };
}
