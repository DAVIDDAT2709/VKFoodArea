namespace VKFoodArea.Web.ViewModels;

public class QrCodeItemIndexViewModel
{
    public PagedListViewModel<QrCodeItemListItemViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int CoveredPoiCount { get; set; }
    public int CoveredTourCount { get; set; }
}
