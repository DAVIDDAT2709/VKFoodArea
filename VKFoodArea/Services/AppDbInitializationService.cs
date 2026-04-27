using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Data;
using VKFoodArea.Helpers;

namespace VKFoodArea.Services;

public sealed class AppDbInitializationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppBuildMetadataService _buildMetadataService;
    private readonly object _gate = new();
    private Task? _initializeTask;

    public AppDbInitializationService(
        IServiceProvider serviceProvider,
        AppBuildMetadataService buildMetadataService)
    {
        _serviceProvider = serviceProvider;
        _buildMetadataService = buildMetadataService;
    }

    public Task EnsureInitializedAsync()
    {
        lock (_gate)
        {
            _initializeTask ??= Task.Run(InitializeInternalAsync);
            return _initializeTask;
        }
    }

    private async Task InitializeInternalAsync()
    {
        AppStartupTrace.Log("AppDbInitializationService.InitializeInternalAsync start");
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AppDataInitializer.InitializeAsync(db, _buildMetadataService.InternalToolsEnabled).ConfigureAwait(false);
        AppStartupTrace.Log("AppDbInitializationService.InitializeInternalAsync complete");
    }
}
