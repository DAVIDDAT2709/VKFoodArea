using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace VKFoodArea.Services;

public class AppRootNavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public AppRootNavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task SetRootAsync<TPage>() where TPage : Page
        => SetRootAsync(_serviceProvider.GetRequiredService<TPage>());

    public Task SetRootAsync(Page page)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var application = Application.Current
                ?? throw new InvalidOperationException("Application.Current is unavailable.");

            var window = application.Windows.FirstOrDefault()
                ?? throw new InvalidOperationException("No active MAUI window is available.");

            window.Page = page is NavigationPage navigationPage
                ? navigationPage
                : new NavigationPage(page);
        });
    }
}
