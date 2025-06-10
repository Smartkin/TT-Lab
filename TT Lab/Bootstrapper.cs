using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using org.ogre;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Rendering;
using TT_Lab.Services;
using TT_Lab.Services.Implementations;
using TT_Lab.Util;
using TT_Lab.ViewModels;
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

        protected override void Configure()
        {
            base.Configure();

            _container.Instance(_container)
                .Singleton<IWindowManager, WindowManager>()
                .Singleton<IEventAggregator, EventAggregator>()
                .Singleton<ProjectManager>()
                .Singleton<OgreWindowManager>()
                .Singleton<IActiveChunkService, ActiveChunkService>()
                .RegisterPerRequest(typeof(IDataValidatorService), nameof(IDataValidatorService), typeof(DataValidatorService));

            foreach (var assembly in SelectAssemblies())
            {
                assembly.GetTypes()
                    .Where(type => type.IsClass)
                    .Where(type => type.Name.EndsWith("ViewModel"))
                    .ToList()
                    .ForEach(viewModelType => _container.RegisterPerRequest(
                        viewModelType, viewModelType.ToString(), viewModelType));
            }
        }

        protected override void StartRuntime()
        {
            base.StartRuntime();

            _container.GetInstance<OgreWindowManager>().Initialize();
#if DEBUG
            org.ogre.LogManager.getSingleton().setMinLogLevel(LogMessageLevel.LML_WARNING);
#else
            org.ogre.LogManager.getSingleton().setMinLogLevel(LogMessageLevel.LML_CRITICAL);
#endif
        }

        protected override Object GetInstance(Type service, String key)
        {
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
