using System;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Dock.Model;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;
using Splat;
using Splat.Microsoft.Extensions.DependencyInjection;
using TT_Lab.Controls;
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
        ComboBoxDropDowns.KeepScrollRequestsInside();
        Styles.Add(ToolTips.FitInWindows());
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

                // Deserialized layouts resolve their panels from the container so they come back as the same singletons
                services.AddSingleton<IDockSerializer>(provider => new Dock.Serializer.DockSerializer(provider));
                services.AddSingleton<DockFactory>();

                services
                    .AddLabServices()
                    .RegisterAllViewsAndViewModels();
            });
        
        _host = hostBuilder.Build();
        _host.Services.UseMicrosoftDependencyResolver();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // An exception escaping a handler, an async void method or a command nothing watches ended TT Lab, unsaved edits and all
            Dispatcher.UIThread.UnhandledException += (_, args) =>
            {
                LogUnexpected(args.Exception);
                args.Handled = true;
            };
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(LogUnexpected);

            var window = new ShellView(Services.GetRequiredService<ILabManager>());

            desktop.MainWindow = window;
            Services.GetRequiredService<Tools.Discord.DiscordPresence>().Start();
            // The context viewports keep current, made before anything unpacks the game's assets (ViewportHost.PinGlLibrary)
            System.Threading.Tasks.Task.Run(ViewportHost.MakeGlAnchor);
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

    private static void LogUnexpected(Exception exception)
    {
        Log.WriteLine($"Something went wrong, TT Lab endures: {exception.GetType().Name}: {exception.Message}", Log.LogType.Error);
        Log.WriteLine(exception.ToString(), Log.LogType.Debug);
        Log.WriteLine("Report this as an issue on GitHub with reproduction steps how you got here!", Log.LogType.Error);
    }
}