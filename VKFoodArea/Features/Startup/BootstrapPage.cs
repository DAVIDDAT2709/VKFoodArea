using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using VKFoodArea.Features.Home;
using VKFoodArea.Helpers;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Startup;

public sealed class BootstrapPage : ContentPage
{
    private readonly IServiceProvider _serviceProvider;
    private bool _started;

    public BootstrapPage(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        BackgroundColor = Color.FromArgb("#0F1513");
        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 18,
                    Children =
                    {
                        new Label
                        {
                            Text = "VKFoodArea",
                            FontSize = 28,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new ActivityIndicator
                        {
                            IsRunning = true,
                            Color = Color.FromArgb("#17C5A3"),
                            WidthRequest = 42,
                            HeightRequest = 42
                        }
                    }
                }
            }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AppStartupTrace.Log("BootstrapPage.OnAppearing");

        if (_started)
            return;

        _started = true;
        _ = RunStartupAsync();
    }

    private async Task RunStartupAsync()
    {
        try
        {
            await Task.Yield();
            AppStartupTrace.Log("BootstrapPage startup task running");
            var dbInitializationService = _serviceProvider.GetRequiredService<AppDbInitializationService>();
            AppStartupTrace.Log("BootstrapPage resolved AppDbInitializationService");
            var authService = _serviceProvider.GetRequiredService<AuthService>();
            AppStartupTrace.Log("BootstrapPage resolved AuthService");
            var settingsService = _serviceProvider.GetRequiredService<AppSettingsService>();
            AppStartupTrace.Log("BootstrapPage resolved AppSettingsService");
            var rootNavigationService = _serviceProvider.GetRequiredService<AppRootNavigationService>();
            AppStartupTrace.Log("BootstrapPage resolved AppRootNavigationService");

            AppStartupTrace.Log("BootstrapPage database init start");
            await dbInitializationService.EnsureInitializedAsync();
            AppStartupTrace.Log("BootstrapPage database init complete");
            AppStartupTrace.Log("BootstrapPage session restore start");
            var restoredSession = await authService.TryRestoreSessionAsync();
            AppStartupTrace.Log($"BootstrapPage session restore complete: {restoredSession}");

            if (restoredSession || settingsService.HasCompletedEntryFlow)
            {
                AppStartupTrace.Log("BootstrapPage navigating to HomeDesignPage");
                await rootNavigationService.SetRootAsync<HomeDesignPage>();
                return;
            }

            AppStartupTrace.Log("BootstrapPage navigating to HomeEntryPage");
            await rootNavigationService.SetRootAsync<HomeEntryPage>();
        }
        catch (Exception ex)
        {
            AppStartupTrace.Log($"BootstrapPage failed: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(24),
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Startup failed",
                            FontSize = 20,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = ex.Message,
                            FontSize = 12,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center
                        }
                    }
                };
            });
        }
    }
}
