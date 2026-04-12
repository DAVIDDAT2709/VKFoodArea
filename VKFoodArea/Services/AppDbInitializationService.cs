using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Data;

namespace VKFoodArea.Services;

public sealed class AppDbInitializationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _gate = new();
    private Task? _initializeTask;

    public AppDbInitializationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AppDataInitializer.InitializeAsync(db).ConfigureAwait(false);
    }
}

