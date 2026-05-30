using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Caliburn.Micro;
using DynamicData;
using GlmSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Silk.NET.Input;
using TT_Lab.Assets;
using TT_Lab.Attributes.Viewport;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Input;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views;
using Twinsanity.TwinsanityInterchange.Common;
using Action = System.Action;
using Screen = Caliburn.Micro.Screen;
using Vector2 = System.Numerics.Vector2;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;

namespace TT_Lab.ViewModels;

public partial class ViewportViewModel : ReactiveObject
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private ViewportObject? _selectedObject;
    
    private readonly SourceCache<ViewportObject, string> _editableObjects;
    private Renderer? _renderer;
    
    private IInputContext? _inputContext;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private Scene? _scene;
    private bool _renderInit;
    private bool _canStartRenderingOnRenderCreation = false;
    private bool _firstRender = true;
    private RenderContext? _renderContext;
    private EditingContext? _editingContext;
    private DocumentViewModel? _document;
    private ivec2 ViewportSize => _renderContext == null ? ivec2.Ones : new ivec2((int)_renderContext.ViewportSize.x, (int)_renderContext.ViewportSize.y);
    private readonly CompositeDisposable _closeDisposables = new();

    public ViewportViewModel()
    {
        _editableObjects = new SourceCache<ViewportObject, String>(x => x.DocumentName);
        _editableObjects.DisposeWith(_closeDisposables);
    }
    
    public void Init(DocumentViewModel document)
    {
        _document = document;

        _editableObjects.Connect().Subscribe(x =>
        {
            foreach (var change in x)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _scene?.AddChild(change.Current.Render);
                        break;
                    case ChangeReason.Remove:
                        _scene?.RemoveChild(change.Current.Render);
                        break;
                }
            }
        }).DisposeWith(_closeDisposables);

        this.WhenAnyValue(x => x._document!.IsReady)
            .Where(x => x)
            .Take(1)
            .Subscribe(_ =>
            {
                if (_renderInit)
                {
                    return;
                }
                
                if (_renderContext == null)
                {
                    _canStartRenderingOnRenderCreation = true;
                    return;
                }
                
                _renderContext.QueueRenderAction(InitScene);
            }).DisposeWith(_closeDisposables);

        this.WhenAnyValue(x => x._document!.Inspector).ObserveOn(RxSchedulers.MainThreadScheduler)
            .WhereNotNull()
            .Subscribe(x =>
            {
                var viewportObject = _editableObjects.Lookup(x.EditorName);
                if (viewportObject is { HasValue: true, Value.Render.IsSelectable: true } && viewportObject.Value != SelectedObject)
                {
                    _editingContext?.Select(viewportObject.Value);
                    SelectedObject = viewportObject.Value;
                }
            }).DisposeWith(_closeDisposables);
    }

    public void FrameResized(SizeChangedEventArgs _)
    {
        CanRender = false;
        this.RaisePropertyChanged(nameof(CanRender));
        this.RaisePropertyChanged(nameof(SceneStatus));
        _renderContext?.QueueRenderAction(() =>
        {
            _renderer?.SetFrameBufferSize(ViewportSize);
            _scene?.UpdateResolution(ViewportSize);
        });
        
        Dispatcher.UIThread.Post(() =>
        {
            if (!_renderInit)
            {
                return;
            }
            
            CanRender = true;
            this.RaisePropertyChanged(nameof(CanRender));
            this.RaisePropertyChanged(nameof(SceneStatus));
        });
    }

    public RenderContext? GetRenderContext()
    {
        return _renderContext;
    }

    public IReadOnlyList<ViewportObject> GetViewportObjects()
    {
        return _editableObjects.Items;
    }

    public void Close()
    {
        _closeDisposables.Dispose();
    }

    public void PrepareRender(RenderRoutedEventArgs renderArgs)
    {
        _renderContext = renderArgs.RenderContext;
        
        _renderContext.QueueRenderAction(() =>
        {
            _renderer = new Renderer(_renderContext);
            _renderer.FinishRender += RendererOnFinishRender;
            _renderer.SceneInitialized += RendererOnSceneInitialized;
            _inputContext = new LabInputContext(_renderer, renderArgs.RenderArea);
            _renderer.InitInput(_inputContext, UseImgui);
        
            _scene = new Scene(_renderContext, "ROOT_SCENE");
            _renderer.RegisterForRendering(_scene.Camera);

            _editingContext = new EditingContext(_renderContext, _scene);

            if (_canStartRenderingOnRenderCreation)
            {
                _renderContext.QueueRenderAction(InitScene);
            }
        
            _mouse = _inputContext.Mice[0];
            _keyboard = _inputContext.Keyboards[0];
        
            _keyboard.KeyDown += KeyboardOnKeyDown;
            _mouse.MouseMove += OnMouseMove;
            _mouse.MouseDown += OnMouseDown;
            _mouse.MouseUp += OnMouseUp;
            _renderer.Update += RendererOnUpdate;

            if (CanRender)
            {
                _renderer.SetFrameBufferSize(ViewportSize);
                _scene.UpdateResolution(ViewportSize);
            }
        });
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (_editingContext == null)
        {
            return;
        }
        
        if (!_editingContext.IsInstanceSelected())
        {
            return;
        }
            
        var pos = mouse.Position;
        _editingContext.EndTransform(pos.X, pos.Y);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_editingContext == null)
        {
            return;
        }
        
        if (button != MouseButton.Left)
        {
            return;
        }
            
        var pos = mouse.Position;
        if ((_editingContext.TransformMode == TransformMode.SELECTION || _editingContext.TransformAxis == TransformAxis.NONE) && !_editingContext.IsInstanceSelected())
        {
            MouseSelect(pos.X, pos.Y);
        }
        else if (_editingContext.IsInstanceSelected())
        {
            _editingContext.StartTransform(pos.X, pos.Y);
        }
    }
    
    private void MouseSelect(float x, float y)
    {
        if (_renderer == null || _scene == null || _keyboard == null || _editingContext == null)
        {
            return;
        }
            
        var rayOrigin = _scene.Camera.GetPosition();
        var rayDirection = _scene.Camera.GetRayFromViewport(x, y);
            
        _editingContext.Deselect();
        ViewportObject? result = null;
        if (!_keyboard.IsKeyPressed(Key.ControlLeft))
        {
            var minDistance = float.MaxValue;
            foreach (var (name, viewportObject) in _editableObjects.KeyValues)
            {
                var instance = viewportObject.Render;
                if (!instance.IsVisible || !instance.IsSelectable)
                {
                    continue;
                }
                
                var hit = new vec3();
                var distance = 0.0f;
                var worldPosition = instance.WorldTransform.Column3.xyz;
                if (!MathExtension.IntersectRayBox(rayOrigin, rayDirection, worldPosition.xyz, instance.GetOffset(),
                        instance.GetSize(), instance.LocalTransform, ref distance, ref hit))
                {
                    continue;
                }

                if (!(distance < minDistance))
                {
                    continue;
                }
                
                result = viewportObject;
                minDistance = distance;
            }
            
            if (result != null)
            {
                _editingContext.Select(result);
                SelectedObject = result;
                if (SelectedObject.Property.PropertyType == typeof(LabURI))
                {
                    _document?.OpenInspector(SelectedObject.Property["[data]"]);
                }
                else
                {
                    _document?.OpenInspector(SelectedObject.Property);
                }
            }
        }
        
        // if (result == null && _colData != null)
        // {
        //     var hit = new vec3();
        //     var minDistance = float.MaxValue;
        //     foreach (var triangle in _colData.Triangles)
        //     {
        //         var hitPos = new vec3();
        //         var distance = float.MaxValue;
        //         var p1 = _colData.Vectors[triangle.Face.Indexes![0]];
        //         var p2 = _colData.Vectors[triangle.Face.Indexes[1]];
        //         var p3 = _colData.Vectors[triangle.Face.Indexes[2]];
        //         if (!MathExtension.IntersectRayTriangle(rayOrigin, rayDirection, new vec3(p1.X, p1.Y, p1.Z),
        //                 new vec3(p2.X, p2.Y, p2.Z), new vec3(p3.X, p3.Y, p3.Z), ref distance, ref hitPos))
        //         {
        //             continue;
        //         }
        //
        //         if (!(distance < minDistance))
        //         {
        //             continue;
        //         }
        //
        //         hit = hitPos;
        //         minDistance = distance;
        //     }
        //
        //     if (!minDistance.Equals(float.MaxValue))
        //     {
        //         _editingContext.SetCursorCoordinates(hit);
        //         if (_keyboard.IsKeyPressed(Key.ControlLeft))
        //         {
        //             _editingContext.SpawnAtCursor();
        //         }
        //     }
        // }
    }

    private void InitScene()
    {
        if (_document == null)
        {
            FinalizeSceneInit();
            return;
        }

        var viewportContext = new ViewportContext(_renderContext!, _editingContext!, _renderer!);

        var viewportObjects = _document.DocumentModel.GetViewportObjects(viewportContext, _document.PropertyGraph.Root);
        foreach (var viewportObject in viewportObjects)
        {
            _editableObjects.AddOrUpdate(viewportObject);
        }
        
        FinalizeSceneInit();
    }

    private void FinalizeSceneInit()
    {
        _renderInit = true;
        
        _renderer!.FireSceneInitialized();
        _renderer.RegisterForRendering(_scene!, true);
        _renderer.RegisterForUpdating(_scene!);

        var camForward = -_scene!.Camera.GetForward();
        _scene.Camera.Translate(camForward * -5);
        
        CanRender = true;
        this.RaisePropertyChanged(nameof(CanRender));
        this.RaisePropertyChanged(nameof(SceneStatus));
    }

    public void TerminateRender()
    {
        CanRender = false;
        this.RaisePropertyChanged(nameof(CanRender));
        this.RaisePropertyChanged(nameof(SceneStatus));
    }

    private void RendererOnSceneInitialized()
    {
        if (!_firstRender)
        {
            return;
        }
        
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
        if (_scene == null || _editingContext == null)
        {
            return;
        }
        
        if (key == Key.T)
        {
            _editingContext.ToggleTranslate();
        }
        else if (key == Key.R)
        {
            _editingContext.ToggleRotate();
        }
        else if (key == Key.E)
        {
            _editingContext.ToggleScale();
        }
        else if (key == Key.X)
        {
            _editingContext.SetTransformAxis(TransformAxis.X);
        }
        else if (key == Key.Y)
        {
            _editingContext.SetTransformAxis(TransformAxis.Y);
        }
        else if (key == Key.Z)
        {
            _editingContext.SetTransformAxis(TransformAxis.Z);
        }
        else if (key == Key.Left)
        {
            _editingContext.MoveCursorGrid(-vec3.UnitX);
        }
        else if (key == Key.Right)
        {
            _editingContext.MoveCursorGrid(vec3.UnitX);
        }
        else if (key == Key.Up)
        {
            _editingContext.MoveCursorGrid(vec3.UnitZ);
        }
        else if (key == Key.Down)
        {
            _editingContext.MoveCursorGrid(-vec3.UnitZ);
        }
        else if (key == Key.PageUp)
        {
            _editingContext.MoveCursorGrid(vec3.UnitY);
        }
        else if (key == Key.PageDown)
        {
            _editingContext.MoveCursorGrid(-vec3.UnitY);
        }
        else if (key == Key.K && _editingContext.SelectedInstance != null)
        {
            _editingContext.SetPalette(_editingContext.SelectedInstance);
        }
        else if (key == Key.P)
        {
            _editingContext.SpawnAtCursor();
        }
        else if (key == Key.G)
        {
            _editingContext.SetGrid();
        }
        else if (key == Key.U)
        {
            _document?.OpenInspector(null);
            _editingContext.Deselect();
        }
        else if (key == Key.L)
        {
            _editingContext.ToggleLocality();
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

        if (_editingContext != null && mouse.IsButtonPressed(MouseButton.Left) && _editingContext.IsInstanceSelected())
        {
            var pos = mousePos;
            _editingContext.UpdateTransform(pos.X, pos.Y);
        }

        _prevMousePosition = mousePos;
    }

    public Action<Renderer, Scene>? SceneInitializer { get; set; }
    public bool CanRender { get; private set; }
    public bool UseImgui { get; set; } = true;
    public string SceneStatus => CanRender ? "" : "Loading scene...";
}

public record ViewportContext(RenderContext RenderContext, EditingContext EditingContext, Renderer Renderer);