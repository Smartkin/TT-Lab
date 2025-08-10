using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using GlmSharp;
using Silk.NET.Input;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Input;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using TT_Lab.Views;
using Action = System.Action;

namespace TT_Lab.ViewModels;

public class ViewportViewModel(RenderContext renderContext) : Screen
{
    private WriteableBitmap? _renderOutput;
    private Renderer? _renderer;
    private Image? _display;
    private ivec2 _viewportSize = ivec2.Ones;
    private IInputContext? _inputContext;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private readonly ConcurrentQueue<Action<Renderer, Scene>> _renderQueue = [];
    private Scene? _scene;
    private bool _renderInit;
    private bool _firstRender = true;

    private void CompositionTargetOnRendering(object? sender, EventArgs e)
    {
        if (!CanRender || _renderOutput == null || _renderer == null || _renderer.IsDisposed || _scene == null)
        {
            return;
        }
        
        _renderer.DoUpdate();
    }

    public void QueueRenderAction(Action<Renderer, Scene> action)
    {
        if (!_renderInit)
        {
            _renderQueue.Enqueue(action);
            return;
        }
        
        renderContext.QueueRenderAction(() =>
        {
            action(_renderer!, _scene!);
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
            _scene?.UpdateResolution(_viewportSize);
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
            dpiX = 96.0 * transform.M11;
            dpiY = 96.0 * transform.M22;
        }
        _renderOutput = new WriteableBitmap(_viewportSize.x, _viewportSize.y, dpiX, dpiY, PixelFormats.Bgra32, null);
        _display = image;
        _display.Source = _renderOutput;

        if (_renderInit)
        {
            return;
        }

        _renderInit = true;
        _renderer = IoC.Get<Renderer>();
        _renderer.FinishRender += RendererOnFinishRender;
        _renderer.SceneInitialized += RendererOnSceneInitialized;
        _scene = new Scene(renderContext, "ROOT_SCENE");
        _renderer.RegisterForRendering(_scene.Camera);
        _inputContext = new LabInputContext(_renderer, _display!);
        _renderer.InitInput(_inputContext);

        while (_renderQueue.TryDequeue(out var action))
        {
            renderContext.QueueRenderAction(() => action.Invoke(_renderer, _scene));
        }

        if (SceneInitializer != null)
        {
            renderContext.QueueRenderAction(() =>
            {
                SceneInitializer(_renderer, _scene);
                _renderer.FireSceneInitialized();
                _renderer.RegisterForRendering(_scene, true);
                _renderer.RegisterForUpdating(_scene);
            });
        }
        
        _mouse = _inputContext.Mice[0];
        _keyboard = _inputContext.Keyboards[0];
            
        _keyboard.KeyDown += KeyboardOnKeyDown;
        _mouse.MouseMove += OnMouseMove;
        _renderer.Update += RendererOnUpdate;

        if (CanRender)
        {
            _renderer.SetFrameBufferSize(_viewportSize);
            _scene.UpdateResolution(_viewportSize);
        }
    }

    private void RendererOnSceneInitialized()
    {
        if (!_firstRender)
        {
            return;
        }
        
        CanRender = true;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        _firstRender = false;
    }

    private void RendererOnFinishRender()
    {
        _scene?.UpdateRenderTransform();
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_renderOutput == null)
            {
                return;
            }
            
            _renderer?.DoUpdate();
            _renderer?.GetRenderImage(_renderOutput);
            _display?.InvalidateVisual();
        });
    }

    public void ViewportLoaded(ViewportView viewport)
    {
        _viewportSize.x = (int)viewport.ActualWidth;
        _viewportSize.y = (int)viewport.ActualHeight;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _renderer?.SetFrameBufferSize(_viewportSize);
            _scene?.UpdateResolution(_viewportSize);
            if (_display != null)
            {
                PrepareRender(_display);
            }
        });
    }
    
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        base.OnActivateAsync(cancellationToken);

        if (!_firstRender)
        {
            CanRender = true;
            NotifyOfPropertyChange(nameof(CanRender));
            NotifyOfPropertyChange(nameof(SceneStatus));
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CompositionTargetEx.FrameUpdating += CompositionTargetOnRendering;
        });
        
        return Task.CompletedTask;
    }

    private void RendererOnUpdate(double delta)
    {
        if (_scene == null || _keyboard == null)
        {
            return;
        }
        
        var camForward = -_scene.Camera.GetForward();
        var camLeft = -_scene.Camera.GetLeft();
        var camSpeed = 10.0f;
        if (_keyboard.IsKeyPressed(Key.ShiftLeft) || _keyboard.IsKeyPressed(Key.ShiftRight))
        {
            camSpeed *= 5.0f;
        }
        if (_keyboard.IsKeyPressed(Key.W))
        {
            _scene.Camera.Translate(camForward * camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.S))
        {
            _scene.Camera.Translate(camForward * -camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.A))
        {
            _scene.Camera.Translate(camLeft * camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.D))
        {
            _scene.Camera.Translate(camLeft * -camSpeed * (float)delta);
        }
    }

    private void KeyboardOnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (_scene == null)
        {
            return;
        }
    }

    private Vector2 _prevMousePosition = new(-1, -1);
    private void OnMouseMove(IMouse mouse, Vector2 mousePos)
    {
        var viewRect = new Rect(0, 0, _viewportSize.x, _viewportSize.y);
        if (!viewRect.Contains(mousePos.X, mousePos.Y))
        {
            return;
        }
        
        if (_prevMousePosition is { X: -1, Y: -1 })
        {
            _prevMousePosition = mousePos;
        }

        if (mouse.IsButtonPressed(MouseButton.Right))
        {
            var delta = (_prevMousePosition - mousePos).FromSystem() * 0.2f;
            var camera = _scene!.Camera;
            var camPosition = camera.GetPosition();
            camera.SetPosition(vec3.Zero);
            camera.Rotate(new vec3(0, glm.Radians(delta.x), 0));
            var left = camera.GetLeft() * 0.05f;
            camera.Rotate(left * delta.y);
            camera.SetPosition(camPosition);
        }

        _prevMousePosition = mousePos;
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        CanRender = false;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            CompositionTargetEx.FrameUpdating -= CompositionTargetOnRendering;
        });

        if (close)
        {
            _renderer?.Dispose();
        }
        
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public Action<Renderer, Scene>? SceneInitializer { get; set; }
    public bool CanRender { get; private set; }
    public string SceneStatus => CanRender ? "" : "Loading scene...";
}