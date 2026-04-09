using VKFoodArea.Features.Auth;

using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    public App(IServiceProvider serviceProvider, AuthService authService)
    {
        InitializeComponent();

        var hasSession = authService.TryRestoreSessionAsync().GetAwaiter().GetResult();
        MainPage = new NavigationPage(
            hasSession
                ? serviceProvider.GetRequiredService<HomeDesignPage>()
                : serviceProvider.GetRequiredService<LoginPage>());
    }
}
