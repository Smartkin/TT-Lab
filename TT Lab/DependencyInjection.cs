using System.Linq;
using System.Reflection;
using Caliburn.Micro;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Splat;
using TT_Lab.Project;
using TT_Lab.Services;
using TT_Lab.Services.Implementations;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab;

public static class DependencyInjection
{
    public static IServiceCollection RegisterAllViewsAndViewModels(this IServiceCollection services)
    {
        Locator.CurrentMutable.RegisterViewsForViewModels(Assembly.GetCallingAssembly());
        
        var assemblies = new [] { Assembly.GetCallingAssembly() };
        foreach (var assembly in assemblies)
        {
            assembly.GetTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => !type.IsInterface)
                .Where(type => type.IsClass)
                .Where(type => type.Name.EndsWith("ViewModel"))
                .ToList()
                .ForEach(viewModelType =>
                {
                    if (viewModelType == typeof(ShellViewModel))
                    {
                        services.AddSingleton<ILabManager, ShellViewModel>();
                        return;
                    }
                        
                    services.AddTransient(viewModelType, viewModelType);
                });
        }
        
        return services;
    }

    public static IServiceCollection AddLabServices(this IServiceCollection services)
    {
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IEventAggregator, EventAggregator>();
        services.AddSingleton<ProjectManager>();
        services.AddSingleton<Rendering.Services.BatchService>();
        services.AddSingleton<Rendering.SceneInstanceFactory>();
        services.AddSingleton<Rendering.RenderContext>();
        services.AddSingleton<Rendering.MeshBuilder>();
        services.AddSingleton<Rendering.TwinSkeletonManager>();
        services.AddSingleton<Rendering.Services.TextureService>();
        services.AddSingleton<Rendering.Factories.MeshFactory>();
        services.AddSingleton<Rendering.Services.MeshService>();
        services.AddSingleton<Rendering.Factories.MaterialFactory>();
        services.AddSingleton<IActiveChunkService, ActiveChunkService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddTransient<IDataValidatorService, DataValidatorService>();
        services.AddTransient<Rendering.Renderer>();
        services.AddTransient<Rendering.Services.PassService>();
        
        return services;
    }
}