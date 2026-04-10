using VKFoodArea.Features.Auth;

using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Home;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthService _authService;
    private readonly LocationTrackingPolicyService _locationTrackingPolicyService;

    public App(
        IServiceProvider serviceProvider,
        AuthService authService,
        LocationTrackingPolicyService locationTrackingPolicyService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _authService = authService;
        _locationTrackingPolicyService = locationTrackingPolicyService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var hasSession = _authService.TryRestoreSessionAsync().GetAwaiter().GetResult();
        Page rootPage =
            hasSession
                ? _serviceProvider.GetRequiredService<HomeDesignPage>()
                : _serviceProvider.GetRequiredService<LoginPage>();

        var window = new Window(new NavigationPage(rootPage));
        _locationTrackingPolicyService.SetAppForeground(true);
        window.Resumed += OnWindowResumed;
        window.Stopped += OnWindowStopped;
        return window;
    }

    private void OnWindowResumed(object? sender, EventArgs e)
        => _locationTrackingPolicyService.SetAppForeground(true);

    private void OnWindowStopped(object? sender, EventArgs e)
        => _locationTrackingPolicyService.SetAppForeground(false);
}
