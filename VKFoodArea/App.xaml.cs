using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Startup;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LocationTrackingPolicyService _locationTrackingPolicyService;

    public App(
        IServiceProvider serviceProvider,
        LocationTrackingPolicyService locationTrackingPolicyService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _locationTrackingPolicyService = locationTrackingPolicyService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var rootPage = _serviceProvider.GetRequiredService<StartupPage>();

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
