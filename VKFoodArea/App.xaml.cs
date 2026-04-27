using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Startup;
using VKFoodArea.Helpers;
using VKFoodArea.Services;

namespace VKFoodArea;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        AppStartupTrace.Log("App.CreateWindow start");
        var window = new Window(new NavigationPage(new BootstrapPage(_serviceProvider)));
        Resolve<LocationTrackingPolicyService>().SetAppForeground(true);
        ScheduleForegroundServices(window);
        window.Resumed += OnWindowResumed;
        window.Stopped += OnWindowStopped;

        var pendingUri = PendingAppLinkStore.Take();
        if (pendingUri is not null)
            ReceiveAppLink(pendingUri);

        AppStartupTrace.Log("App.CreateWindow complete");
        return window;
    }

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        Resolve<LocationTrackingPolicyService>().SetAppForeground(true);

        if (sender is Window window)
        {
            ScheduleForegroundServices(window);
            return;
        }

        StartForegroundServices();
    }

    private void OnWindowStopped(object? sender, EventArgs e)
    {
        Resolve<LocationTrackingPolicyService>().SetAppForeground(false);
        _ = Resolve<AppDevicePresenceService>().SetAppForegroundAsync(false);
    }

    public void ReceiveAppLink(Uri uri)
    {
        Resolve<AppLinkService>().Enqueue(uri);

        var window = Windows.FirstOrDefault();
        if (window?.Dispatcher is { } dispatcher)
        {
            dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(700),
                () => _ = Resolve<AppLinkService>().TryHandlePendingAsync());
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(700).ConfigureAwait(false);
            await Resolve<AppLinkService>().TryHandlePendingAsync().ConfigureAwait(false);
        });
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        ReceiveAppLink(uri);
    }

    private void ScheduleForegroundServices(Window window)
    {
        if (window.Dispatcher is { } dispatcher)
        {
            dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(500),
                StartForegroundServices);
            return;
        }

        StartForegroundServices();
    }

    private void StartForegroundServices()
    {
        _ = Resolve<AppDevicePresenceService>().SetAppForegroundAsync(true);
        _ = Resolve<AuthService>().RefreshCurrentUserAccessAsync();
        Resolve<AppSyncOutboxService>().FlushPendingInBackground();
    }

    private T Resolve<T>() where T : notnull
        => _serviceProvider.GetRequiredService<T>();
}
