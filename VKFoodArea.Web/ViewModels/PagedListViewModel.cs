namespace VKFoodArea.Web.ViewModels;

public class PagedListViewModel<T>
{
    public const int DefaultPageSize = 8;

    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public int TotalItems { get; set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));

    public static PagedListViewModel<T> Create(IEnumerable<T> source, int page, int pageSize = DefaultPageSize)
    {
        var allItems = source.ToList();
        var normalizedPageSize = Math.Max(1, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(allItems.Count / (double)normalizedPageSize));
        var normalizedPage = Math.Clamp(page, 1, totalPages);

        return new PagedListViewModel<T>
        {
            Items = allItems
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = allItems.Count
        };
    }
}
