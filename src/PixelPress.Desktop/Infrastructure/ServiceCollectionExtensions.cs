using Microsoft.Extensions.DependencyInjection;
using PixelPress.Core.Execution;
using PixelPress.Core.Planning;
using PixelPress.Core.Services;
using PixelPress.Desktop.ViewModels;

namespace PixelPress.Desktop.Infrastructure;

/// <summary>
/// The composition root. Every service and view model registration in the
/// application lives here and nowhere else, so the object graph can be
/// read in one place. Core types are registered as they arrive in later
/// milestones (planner in M2, engine in M3, settings store in M6).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Engine
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<JobPlanner>();
        services.AddSingleton<JobExecutor>();

        // View models
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
