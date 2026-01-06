using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;

namespace TT_Lab;

public partial class App : Application
{
    private IHost _host;
    public IServiceProvider Services => _host.Services;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.UseMicrosoftDependencyResolver();
                var resolver = Locator.CurrentMutable;
                resolver.InitializeSplat();
                resolver.InitializeReactiveUI();
                
                resolver.RegisterConstant(new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));
                resolver.RegisterConstant(new AutoDataTemplateBindingHook(), typeof(IPropertyBindingHook));
                RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;

                services
                    .AddLabServices()
                    .RegisterAllViewsAndViewModels();
            });
        
        _host = hostBuilder.Build();
        _host.Services.UseMicrosoftDependencyResolver();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new ShellView();
            var vm = Services.GetRequiredService<ILabManager>();
            window.DataContext = vm;
            
            desktop.MainWindow = window;
            desktop.Startup += (_, _) =>
            {
                Console.WriteLine("Application started");
            };
            desktop.Exit += async (_, _) =>
            {
                using (_host)
                {
                    await _host.StopAsync();
                }
            };
        }
        
        base.OnFrameworkInitializationCompleted();
    }
}