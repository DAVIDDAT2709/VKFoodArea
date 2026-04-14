using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Startup;
using VKFoodArea.Helpers;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LocationTrackingPolicyService _locationTrackingPolicyService;
    private readonly AppLinkService _appLinkService;

    public App(
        IServiceProvider serviceProvider,
        LocationTrackingPolicyService locationTrackingPolicyService,
        AppLinkService appLinkService)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _locationTrackingPolicyService = locationTrackingPolicyService;
        _appLinkService = appLinkService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var rootPage = _serviceProvider.GetRequiredService<StartupPage>();

        var window = new Window(new NavigationPage(rootPage));
        _locationTrackingPolicyService.SetAppForeground(true);
        window.Resumed += OnWindowResumed;
        window.Stopped += OnWindowStopped;

        var pendingUri = PendingAppLinkStore.Take();
        if (pendingUri is not null)
            ReceiveAppLink(pendingUri);

        return window;
    }

    private void OnWindowResumed(object? sender, EventArgs e)
        => _locationTrackingPolicyService.SetAppForeground(true);

    private void OnWindowStopped(object? sender, EventArgs e)
        => _locationTrackingPolicyService.SetAppForeground(false);

    public void ReceiveAppLink(Uri uri)
    {
        _appLinkService.Enqueue(uri);
        _ = _appLinkService.TryHandlePendingAsync();
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        ReceiveAppLink(uri);
    }
}
