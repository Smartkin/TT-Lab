using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using TT_Lab.Controls;
using TT_Lab.Rendering;

namespace TT_Lab.Views.Editors
{
    public delegate void SceneEditorRoutedEventHandler(object sender, SceneEditorRoutedEventArgs args);
    public class SceneEditorRoutedEventArgs : RoutedEventArgs
    {

        public EmbededRender EmbeddedWindow { get; private set; }

        public SceneEditorRoutedEventArgs(RoutedEvent routedEvent, EmbededRender window)
            : base(routedEvent)
        {
            EmbeddedWindow = window;
        }
    }

    /// <summary>
    /// Interaction logic for SceneEditorView.xaml
    /// </summary>
    public partial class SceneEditorView : System.Windows.Controls.UserControl
    {
        private EmbededRender? _renderer;
        private Window? _mainWindow;
        private Point _placeholderPosition;
        private bool _isListeningToInput = false;

        public SceneEditorView()
        {
            InitializeComponent();

            GlControlView.Loaded += GlControlView_Loaded;

            // LayoutUpdated += SceneEditorView_LayoutUpdated;
            SizeChanged += SceneEditorView_SizeChanged;
            Unloaded += SceneEditorView_Unloaded;
        }

        private void SceneEditorView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RepositionRenderer();
        }

        private void SceneEditorView_LayoutUpdated(Object? sender, EventArgs e)
        {
            RepositionRenderer();
        }

        private void GlControlView_Loaded(System.Object sender, RoutedEventArgs e)
        {
            _mainWindow = Window.GetWindow(this);
            _mainWindow.LocationChanged += MainWindow_LocationChanged;
            _mainWindow.StateChanged += MainWindow_StateChanged;
            _mainWindow.SizeChanged += MainWindow_SizeChanged;

            var windowCreated = _renderer != null;
            if (_renderer == null)
            {
                windowCreated = CreateRenderWindow();
            }
            else if (_renderer != null)
            {
                RepositionRenderer();
                _renderer.Show();
            }

            if (!windowCreated)
            {
                return;
            }

            StartListeningToInput();
        }

        private bool CreateRenderWindow()
        {
            if (!GlControlView.IsDescendantOf(_mainWindow!))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Start();
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    CreateRenderWindow();
                };
                return false;
            }
            
            _placeholderPosition = GlControlView.TransformToAncestor(_mainWindow!).Transform(new Point(0, 0));
            _renderer = new EmbededRender
            {
                Owner = _mainWindow,
                Width = Math.Ceiling(GlControlView.ActualWidth),
                Height = Math.Ceiling(GlControlView.ActualHeight),
                Top = _mainWindow.Top + _placeholderPosition.Y,
                Left = _mainWindow.Left + _placeholderPosition.X,
            };
            _renderer.Show();
            RaiseEvent(new SceneEditorRoutedEventArgs(SceneEditorInitializedEvent, _renderer));
            StartListeningToInput();
            RepositionRenderer();
            return true;
        }

        private void StartListeningToInput()
        {
            if (_isListeningToInput)
            {
                return;
            }
            
            _renderer!.KeyUp += _renderer_KeyUp;
            _renderer.KeyDown += _renderer_KeyDown;
            _renderer.MouseDown += _renderer_MouseDown;
            _renderer.MouseUp += _renderer_MouseUp;
            _renderer.MouseMove += _renderer_MouseMove;
            _renderer.MouseWheel += _renderer_MouseWheel;
            _isListeningToInput = true;
        }

        private void _renderer_MouseUp(Object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RaiseEvent(e);
        }

        private void _renderer_MouseWheel(Object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            RaiseEvent(e);
        }

        private void _renderer_MouseMove(Object sender, System.Windows.Input.MouseEventArgs e)
        {
            RaiseEvent(e);
        }

        private void _renderer_MouseDown(Object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RaiseEvent(e);
        }

        private void _renderer_KeyDown(Object sender, System.Windows.Input.KeyEventArgs e)
        {
            RaiseEvent(e);
        }

        private void _renderer_KeyUp(Object sender, System.Windows.Input.KeyEventArgs e)
        {
            RaiseEvent(e);
        }

        private void MainWindow_SizeChanged(Object sender, SizeChangedEventArgs e)
        {
            RepositionRenderer();
        }

        private void MainWindow_StateChanged(Object? sender, EventArgs e)
        {
            RepositionRenderer();
        }

        private void MainWindow_LocationChanged(Object? sender, EventArgs e)
        {
            RepositionRenderer();
        }

        private void RepositionRenderer()
        {
            if (_renderer == null)
            {
                return;
            }

            var mainWindow = Window.GetWindow(this);
            if (mainWindow == null)
            {
                return;
            }
            
            Debug.WriteLine("Reposition triggered");

            var windowPos = GetWindowsActualPosition(mainWindow);
            _placeholderPosition = GlControlView.TransformToAncestor(mainWindow).Transform(new Point(0, 0));
            _renderer.Width = Math.Ceiling(GlControlView.ActualWidth);
            _renderer.Height = Math.Ceiling(GlControlView.ActualHeight);
            _renderer.Top = windowPos.Y + _placeholderPosition.Y;
            _renderer.Left = windowPos.X + _placeholderPosition.X;
            _renderer.GetRenderWindow()?.NotifyWindowChanged();
        }

        private Point GetWindowsActualPosition(Window window)
        {
            // HACK: Due to how window updates its Top and Left public properties we are forced to use reflection when it's maximized. THANKS MICROSOFT
            if (window.WindowState == WindowState.Maximized)
            {
                var left = typeof(Window).GetField("_actualLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var top = typeof(Window).GetField("_actualTop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                return new Point((double)left!.GetValue(window)!, (double)top!.GetValue(window)!);
            }

            return new Point(window.Left, window.Top);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }

        private void SceneEditorView_Unloaded(System.Object sender, RoutedEventArgs e)
        {
            Debug.Assert(_mainWindow != null);

            _mainWindow.LocationChanged -= MainWindow_LocationChanged;
            _mainWindow.StateChanged -= MainWindow_StateChanged;
            _mainWindow.SizeChanged -= MainWindow_SizeChanged;
            _mainWindow = null;
            _isListeningToInput = false;

            if (_renderer != null)
            {
                _renderer.MouseUp -= _renderer_MouseUp;
                _renderer.MouseDown -= _renderer_MouseDown;
                _renderer.MouseMove -= _renderer_MouseMove;
                _renderer.MouseWheel -= _renderer_MouseWheel;
                _renderer.KeyUp -= _renderer_KeyUp;
                _renderer.KeyDown -= _renderer_KeyDown;
                _renderer.Hide();
            }
        }

        [Description("Called when rendering window has been initialized"), Category("Embedded Renderer")]
        public event SceneEditorRoutedEventHandler SceneEditorInitialized
        {
            add => AddHandler(SceneEditorInitializedEvent, value);
            remove => RemoveHandler(SceneEditorInitializedEvent, value);
        }
        private static readonly RoutedEvent SceneEditorInitializedEvent = EventManager.RegisterRoutedEvent("SceneEditorInitialized",
            RoutingStrategy.Direct, typeof(SceneEditorRoutedEventHandler), typeof(SceneEditorView));
    }
}
