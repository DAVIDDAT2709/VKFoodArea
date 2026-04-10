using VKFoodArea.Features.Auth;

using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthService _authService;

    public App(IServiceProvider serviceProvider, AuthService authService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _authService = authService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var hasSession = _authService.TryRestoreSessionAsync().GetAwaiter().GetResult();
        Page rootPage =
            hasSession
                ? _serviceProvider.GetRequiredService<HomeDesignPage>()
                : _serviceProvider.GetRequiredService<LoginPage>();

        return new Window(new NavigationPage(rootPage));
    }
}
