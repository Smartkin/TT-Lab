using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TT_Lab.Project;
using TT_Lab.Services;
using TT_Lab.Services.Implementations;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Interfaces;
using LogManager = Caliburn.Micro.LogManager;

namespace TT_Lab
{
    public class Bootstrapper : BootstrapperBase
    {
        private readonly SimpleContainer _container = new();

        public Bootstrapper()
        {
            Initialize();
            
            var filters = new List<string>
            {
                "MouseMoved",
                "DragEntered",
                "MouseWheelMoved",
                "RendererRender",
                "DragDropped",
                "LogViewerScroll",
                "AssetBlockMouseMove",
                "AssetBlockMouseDown"
            };
            LogManager.GetLog = type => new Logger(type, filters);
        }

        protected override void OnExit(object sender, EventArgs e)
        {
            _container.GetInstance<Rendering.RenderContext>().ShutdownRender();
            
            base.OnExit(sender, e);
        }

        protected override void StartRuntime()
        {
            base.StartRuntime();
            
            _container.GetInstance<Rendering.RenderContext>().InitRenderApi();
        }

        protected override void Configure()
        {
            base.Configure();

            _container.Instance(_container)
                .Singleton<IWindowManager, WindowManager>()
                .Singleton<IEventAggregator, EventAggregator>()
                .Singleton<ProjectManager>()
                .Singleton<Rendering.Services.BatchService>()
                .Singleton<Rendering.SceneInstanceFactory>()
                .Singleton<Rendering.RenderContext>()
                .Singleton<Rendering.MeshBuilder>()
                .Singleton<Rendering.TwinSkeletonManager>()
                .Singleton<Rendering.Services.TextureService>()
                .Singleton<Rendering.Factories.MeshFactory>()
                .Singleton<Rendering.Services.MeshService>()
                .Singleton<Rendering.Factories.MaterialFactory>()
                .Singleton<IActiveChunkService, ActiveChunkService>()
                .RegisterPerRequest(typeof(IDataValidatorService), nameof(IDataValidatorService), typeof(DataValidatorService));
            _container.RegisterPerRequest(typeof(Rendering.Renderer), nameof(Rendering.Renderer), typeof(Rendering.Renderer));
            _container.RegisterPerRequest(typeof(Rendering.Services.PassService), nameof(Rendering.Services.PassService), typeof(Rendering.Services.PassService));

            foreach (var assembly in SelectAssemblies())
            {
                assembly.GetTypes()
                    .Where(type => type.IsClass)
                    .Where(type => type.Name.EndsWith("ViewModel"))
                    .ToList()
                    .ForEach(viewModelType =>
                    {
                        if (viewModelType == typeof(ShellViewModel))
                        {
                            _container.Singleton<ILabManager, ShellViewModel>();
                            return;
                        }
                        
                        _container.RegisterPerRequest(
                            viewModelType, viewModelType.ToString(), viewModelType);
                    });
            }
        }

        protected override Object GetInstance(Type service, String key)
        {
            // ILabManager is registered specifically to not expose all the public methods for WPF bindings in actual root view model
            if (service == typeof(ShellViewModel))
            {
                service = typeof(ILabManager);
            }
            
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<Object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(Object instance)
        {
            _container.BuildUp(instance);
        }

        protected override async void OnStartup(Object sender, StartupEventArgs e)
        {
            await DisplayRootViewForAsync<ShellViewModel>();
        }
    }
}
