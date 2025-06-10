using Caliburn.Micro;
using GlmSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TT_Lab.Controls;
using TT_Lab.Rendering;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Views.Editors;

namespace TT_Lab.ViewModels.Editors
{
    public class SceneEditorViewModel : Conductor<object>
    {
        private OgreWindow? _window;
        private EmbededRender? _render;
        private string _sceneHeader;
        private IWindowManager windowManager;
        private List<IInputListener> _inputListeners = new();
        private bool _mouseMoveLocked = false;

        public event EventHandler<FileDropEventArgs>? FileDrop;
        public event EventHandler<MouseWheelEventArgs>? OnMouseWheelScrolled;
        public event EventHandler<MouseEventArgs>? OnMouseMoved;
        public event EventHandler<MouseButtonEventArgs>? OnMouseButtonPressed;
        public event EventHandler<MouseButtonEventArgs>? OnMouseButtonPressedUp;
        public event EventHandler<KeyEventArgs>? OnKeyPressed;
        public event EventHandler<KeyEventArgs>? OnKeyPressedUp;

        public EmbededRender? RenderControl
        {
            get => _render;
            private set => _render = value;
        }

        public Action<OgreWindow>? SceneCreator
        {
            private get;
            set;
        }

        public string SceneHeaderModel
        {
            get => _sceneHeader;
            set
            {
                _sceneHeader = value;
                NotifyOfPropertyChange();
            }
        }

        public SceneEditorViewModel(IWindowManager windowManager)
        {
            this.windowManager = windowManager;
            _sceneHeader = "Scene viewer";
        }

        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            _window?.SetHidden(false);

            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(System.Boolean close, CancellationToken cancellationToken)
        {
            if (close && RenderControl != null)
            {
                RenderControl.Cleanup();
                RenderControl = null;
            }
            else
            {
                _window?.SetHidden(true);
            }

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public void AddInputListener(IInputListener listener)
        {
            _inputListeners.Add(listener);
        }

        public void RemoveInputListener(IInputListener listener)
        {
            _inputListeners.Remove(listener);
        }

        public void DragDrop(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                FileDrop?.Invoke(this, new FileDropEventArgs { File = files[0] });
            }
            else if (e.Data.GetDataPresent(typeof(DraggedData)))
            {
                var data = e.Data.GetData(typeof(DraggedData)) as DraggedData;
                FileDrop?.Invoke(this, new FileDropEventArgs { Data = data });
            }
        }

        public void DragEntered(System.Boolean allowDrop, DragEventArgs e)
        {
            if (allowDrop)
            {
                e.Effects = DragDropEffects.All;
            }
        }

        public void MouseWheelMoved(MouseWheelEventArgs e)
        {
            if (_window == null)
            {
                return;
            }
            
            var curMousePos = e.GetPosition(_render);
            if (_window.HandleMouseWheel(curMousePos, e))
            {
                return;
            }

            _ = _inputListeners.Any(listener => listener.MouseWheel(this, e));
        }

        public void LockMouseMove()
        {
            _mouseMoveLocked = true;
        }

        public void UnlockMouseMove()
        {
            _mouseMoveLocked = false;
        }

        public void MouseMoved(MouseEventArgs e)
        {
            if (_window == null)
            {
                return;
            }
            
            var curMousePos = e.GetPosition(_render);
            if (!_mouseMoveLocked)
            {
                if (_window.HandleMouseMove(curMousePos, e))
                {
                    return;
                }
            }

            _ = _inputListeners.Any(listener => listener.MouseMove(this, e));
        }

        public void MousePressed(MouseButtonEventArgs e)
        {
            if (_window == null)
            {
                return;
            }
            
            var curMousePos = e.GetPosition(_render);
            if (_window.HandleMouseInput(curMousePos, e))
            {
                return;
            }
            
            _ = _inputListeners.Any(listener => listener.MouseDown(this, e));
        }
        
        public void MousePressedUp(MouseButtonEventArgs e)
        {
            if (_window == null)
            {
                return;
            }
            
            var curMousePos = e.GetPosition(_render);
            if (_window.HandleMouseInput(curMousePos, e))
            {
                return;
            }
            
            _ = _inputListeners.Any(listener => listener.MouseUp(this, e));
        }

        public void KeyPressed(KeyEventArgs e)
        {
            if (_window == null)
            {
                return;
            }

            if (_window.HandleKeyboardInput(e))
            {
                return;
            }
            
            _ = _inputListeners.Any(listener => listener.KeyPressed(this, e));
        }
        
        public void KeyPressedUp(KeyEventArgs e)
        {
            if (_window == null)
            {
                return;
            }

            if (_window.HandleKeyboardInput(e))
            {
                return;
            }
            
            _ = _inputListeners.Any(listener => listener.KeyReleased(this, e));
        }

        public void ResetScene()
        {
            if (_window == null || _window.IsClosed() || SceneCreator == null)
            {
                return;
            }

            _window.ResetScene();
            _window.EditScene(SceneCreator);
        }

        public void RendererInitialized(SceneEditorRoutedEventArgs embededRender)
        {
            if (_window == null || _window.IsClosed())
            {
                _render = embededRender.EmbeddedWindow;
                _window = embededRender.EmbeddedWindow.GetRenderWindow();

                if (SceneCreator == null)
                {
                    Debug.WriteLine("WARNING: SceneCreator is not set! NO SCENE WILL BE RENDERED");
                }

                if (_window != null && SceneCreator != null)
                {
                    _window.EditScene(SceneCreator);
                }
            }
        }
    }
}
