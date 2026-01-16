using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Caliburn.Micro;
using GlmSharp;
using Silk.NET.Input;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Input;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using TT_Lab.Views;
using Action = System.Action;
using Screen = Caliburn.Micro.Screen;

namespace TT_Lab.ViewModels;

public class ViewportViewModel : Screen
{
    private WriteableBitmap? _renderOutput;
    private Renderer? _renderer;
    private Image? _display;
    
    private IInputContext? _inputContext;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private Scene? _scene;
    private bool _renderInit;
    private bool _firstRender = true;
    private RenderContext? _renderContext;
    private ivec2 ViewportSize => _renderContext == null ? ivec2.Ones : new ivec2((int)_renderContext.ViewportSize.x, (int)_renderContext.ViewportSize.y);
    
    private void CompositionTargetOnRendering(object? sender, EventArgs e)
    {
        if (!CanRender || _renderOutput == null || _renderer == null || _renderer.IsDisposed || _scene == null)
        {
            return;
        }
        
        _renderer.DoUpdate();
    }

    public void FrameResized(SizeChangedEventArgs newSize)
    {
        CanRender = false;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
        Dispatcher.UIThread.Post(() =>
        {
            _renderer?.SetFrameBufferSize(ViewportSize);
            _scene?.UpdateResolution(ViewportSize);
 
            CanRender = true;
            NotifyOfPropertyChange(nameof(CanRender));
            NotifyOfPropertyChange(nameof(SceneStatus));
        });
    }

    public void PrepareRender(RenderRoutedEventArgs renderArgs)
    {
        _renderContext = renderArgs.RenderContext;
        _renderer = new Renderer(_renderContext);
        _renderer.FinishRender += RendererOnFinishRender;
        _renderer.SceneInitialized += RendererOnSceneInitialized;
        _scene = new Scene(_renderContext, "ROOT_SCENE");
        _renderer.RegisterForRendering(_scene.Camera);
        _inputContext = new LabInputContext(_renderer, renderArgs.RenderArea);
        _renderer.InitInput(_inputContext, UseImgui);

        if (SceneInitializer != null)
        {
            _renderContext.QueueRenderAction(() =>
            {
                SceneInitializer(_renderer, _scene);
                _renderer.FireSceneInitialized();
                _renderer.RegisterForRendering(_scene, true);
                _renderer.RegisterForUpdating(_scene);

                var camForward = -_scene.Camera.GetForward();
                _scene.Camera.Translate(camForward * -5);
            });
        }
        
        _mouse = _inputContext.Mice[0];
        _keyboard = _inputContext.Keyboards[0];
        
        _keyboard.KeyDown += KeyboardOnKeyDown;
        _mouse.MouseMove += OnMouseMove;
        _renderer.Update += RendererOnUpdate;

        if (CanRender)
        {
            _renderer.SetFrameBufferSize(ViewportSize);
            _scene.UpdateResolution(ViewportSize);
        }
    }

    public void TerminateRender()
    {
        _renderer?.Dispose();
        
        CanRender = false;
        NotifyOfPropertyChange(nameof(CanRender));
        NotifyOfPropertyChange(nameof(SceneStatus));
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
        Dispatcher.UIThread.Post(() =>
        {
            _renderer?.DoUpdate();
        });
    }
    
    protected override Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        base.OnActivatedAsync(cancellationToken);

        if (!_firstRender)
        {
            CanRender = true;
            NotifyOfPropertyChange(nameof(CanRender));
            NotifyOfPropertyChange(nameof(SceneStatus));
        }
        
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
            _scene.Camera.Translate(camLeft * -camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.D))
        {
            _scene.Camera.Translate(camLeft * camSpeed * (float)delta);
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
        var viewRect = new Rect(0, 0, ViewportSize.x, ViewportSize.y);
        if (!viewRect.Contains(new Point(mousePos.X, mousePos.Y)))
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
            camera.Rotate(new vec3(0, glm.Radians(-delta.x), 0));
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
        
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public Action<Renderer, Scene>? SceneInitializer { get; set; }
    public bool CanRender { get; private set; }
    public bool UseImgui { get; set; } = true;
    public string SceneStatus => CanRender ? "" : "Loading scene...";
}