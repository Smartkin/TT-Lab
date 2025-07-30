using Caliburn.Micro;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TT_Lab.Rendering;

namespace TT_Lab.Controls
{
    public delegate void EmbedRenderRoutedEventHandler(object sender, EmbedRenderRoutedEventArgs args);
    public class EmbedRenderRoutedEventArgs : RoutedEventArgs
    {
        public OgreWindow EmbeddedWindow { get; private set; }

        public EmbedRenderRoutedEventArgs(RoutedEvent routedEvent, OgreWindow window)
            : base(routedEvent)
        {
            EmbeddedWindow = window;
        }
    }

    /// <summary>
    /// Interaction logic for EmbededRender.xaml
    /// </summary>
    public partial class EmbededRender : Window
    {
        private OgreWindow window;
        private OgreWindowManager windowManager;

        public EmbededRender()
        {
            InitializeComponent();

            if (DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }
            
            Loaded += EmbededRender_Loaded;
            Unloaded += EmbededRender_Unloaded;
            windowManager = IoC.Get<OgreWindowManager>();
        }

        // private void CompositionTargetOnRendering(object? sender, EventArgs e)
        // {
        //     Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        // }

        public OgreWindow GetRenderWindow()
        {
            return window;
        }

        /// <summary>
        /// Handles the Loaded event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="routedEventArgs">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
        private void EmbededRender_Loaded(object sender, RoutedEventArgs routedEventArgs)
        {
            SizeChanged += EmbededRender_SizeChanged;
            LocationChanged += EmbededRender_LocationChanged;
            // CompositionTarget.Rendering += CompositionTargetOnRendering;

            NotifyOrInitiliazeRenderWindow((int)RenderSize.Width, (int)RenderSize.Height);
            window.SetVisibility(true);
        }

        private void EmbededRender_LocationChanged(Object? sender, EventArgs e)
        {
            window.NotifyWindowChanged();
        }

        /// <summary>
        /// Handles the Unloaded event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="routedEventArgs">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
        private void EmbededRender_Unloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            if (!window.IsClosed())
            {
                window.SetVisibility(false);
            }

            // CompositionTarget.Rendering -= CompositionTargetOnRendering;
            LocationChanged -= EmbededRender_LocationChanged;
            SizeChanged -= EmbededRender_SizeChanged;
        }

        /// <summary>
        /// Handles the SizeChanged event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.SizeChangedEventArgs"/> Instance containing the event data.</param>
        void EmbededRender_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            NotifyOrInitiliazeRenderWindow((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        private bool wasInitialized = false;
        /// <summary>
        /// This method is used to set the dimensions and the viewport of the EmbedContext control.
        /// </summary>
        /// <param name="width">The width of the EmbedContext drawing area.</param>
        /// <param name="height">The height of the EmbedContext drawing area.</param>
        private void NotifyOrInitiliazeRenderWindow(int width, int height)
        {
            if (!wasInitialized)
            {
                wasInitialized = true;
                window = windowManager.CreateWindow(new WindowInteropHelper(this).Handle, (uint)width, (uint)height);
                window.SetOwner(this);
                RaiseEvent(new EmbedRenderRoutedEventArgs(EmbedRenderInitializedEvent, window));
            }

            window.NotifyWindowChanged();
        }

        public void Cleanup()
        {
            windowManager.CloseWindow(window);
            Close();
        }

        [Description("Called when rendering window has been initialized"), Category("Embedded Renderer")]
        public event EmbedRenderRoutedEventHandler EmbedRenderInitialized
        {
            add => AddHandler(EmbedRenderInitializedEvent, value);
            remove => RemoveHandler(EmbedRenderInitializedEvent, value);
        }
        private static readonly RoutedEvent EmbedRenderInitializedEvent = EventManager.RegisterRoutedEvent("EmbedRenderInitialized",
            RoutingStrategy.Direct, typeof(EmbedRenderRoutedEventHandler), typeof(EmbededRender));
    }
}
