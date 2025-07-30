using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GlmSharp;
using TT_Lab.Rendering;
using TT_Lab.Views;
using Action = System.Action;

namespace TT_Lab.ViewModels;

public class ViewportViewModel(RenderContext renderContext) : Screen
{
    private WriteableBitmap? _renderOutput;
    private Renderer? _renderer;
    private Image? _display;
    private ivec2 _viewportSize = ivec2.Ones;
    private static readonly object RenderLock = new();

    private void CompositionTargetOnRendering(object? sender, EventArgs e)
    {
        if (!CanRender || _renderOutput == null || _renderer == null || _renderer.IsDisposed)
        {
            return;
        }

        // Not sure if it's needed but just as a safety measure we are going to have only 1 viewport access rendering context at a time
        lock (RenderLock)
        {
            _renderer.DoRender();
        }

        _renderOutput.Lock();
        _renderer.GetRenderImage(_renderOutput);
        _renderOutput.Unlock();

        if (_display != null)
        {
            Execute.OnUIThread(_display.InvalidateVisual);
        }
    }

    public void QueueRenderAction(Action<Renderer> action)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_renderer == null || _renderer.IsDisposed)
            {
                return;
            }
            
            action(_renderer);
        });
    }

    public void FrameResized(SizeChangedEventArgs newSize)
    {
        CanRender = false;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        
        _viewportSize.x = (int)newSize.NewSize.Width;
        _viewportSize.y = (int)newSize.NewSize.Height;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _renderer?.SetFrameBufferSize(_viewportSize);
            if (_display != null)
            {
                PrepareRender(_display);
            }
            
            CanRender = true;
            NotifyOfPropertyChange(nameof(CanRender));
            NotifyOfPropertyChange(nameof(SceneStatus));
        });
    }

    public void PrepareRender(Image image)
    {
        var source = PresentationSource.FromVisual(image);
        var dpiX = 96.0;
        var dpiY = 96.0;
        if (source?.CompositionTarget != null)
        {
            var transform = source.CompositionTarget.TransformToDevice;
            dpiX = 96.0 *  transform.M11;
            dpiY = 96.0 *  transform.M22;
        }
        _renderOutput = new WriteableBitmap(_viewportSize.x, _viewportSize.y, dpiX, dpiY, PixelFormats.Bgra32, null);
        _display = image;
        _display.Source = _renderOutput;
    }

    public void ViewportLoaded(ViewportView viewport)
    {
        _viewportSize.x = (int)viewport.ActualWidth;
        _viewportSize.y = (int)viewport.ActualHeight;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _renderer?.SetFrameBufferSize(_viewportSize);
            if (_display != null)
            {
                PrepareRender(_display);
            }
            
            CanRender = true;
            NotifyOfPropertyChange(nameof(CanRender));
            NotifyOfPropertyChange(nameof(SceneStatus));
        });
    }
    
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        base.OnActivateAsync(cancellationToken);
        
        CanRender = true;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CompositionTarget.Rendering += CompositionTargetOnRendering;
        });
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_renderer is { IsDisposed: false })
            {
                return;
            }
            
            _renderer = new Renderer(renderContext);

            if (CanRender)
            {
                _renderer.SetFrameBufferSize(_viewportSize);
            }
        });
        
        return Task.CompletedTask;
    }
    
    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        CanRender = false;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CompositionTarget.Rendering -= CompositionTargetOnRendering;
        });

        if (close)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _renderer?.Dispose();
            });
        }
        
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public bool CanRender { get; private set; }
    public string SceneStatus => CanRender ? "" : "Loading scene...";
}