using org.ogre;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using GlmSharp;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Libraries;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Math = org.ogre.Math;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace TT_Lab.Rendering
{
    /// <summary>
    /// DO NOT CREATE THIS CLASS DIRECTLY. USE <see cref="OgreWindowManager"/> TO DO THAT
    /// </summary>
    public partial class OgreWindow
    {
        private IntPtr _hwnd;

        private NativeWindowPair _internalWindow;
        private Root _root;
        private CameraMan _camera;
        private Camera _mainCamera;
        private SceneManager _sceneManager;
        private bool _mousePositionIsInvalid = true;
        private Point _mousePrevPos;
        private bool _isClosed = false;
        private bool _isResizing = false;
        private bool _canLeftClickToMoveCamera = true;
        private readonly List<InputListener> _inputListeners = new();
        private EmbededRender _owner;

        public event EventHandler? OnRender;

        public OgreWindow(Root root, IntPtr parentWindowHandle)
        {
            _root = root;
            _hwnd = parentWindowHandle;
        }

        public void SetInternalWindow(NativeWindowPair internalWindow)
        {
            _internalWindow = internalWindow;
        }

        public void SetOwner(EmbededRender owner)
        {
            _owner = owner;
        }

        public NativeWindowPair GetNative()
        {
            return _internalWindow;
        }

        public void SetVisibility(bool visible)
        {
            _internalWindow.render.setVisible(visible);
        }

        public bool HandleKeyboardInput(KeyEventArgs keyEvent)
        {
            const int DOWN_EVENT_TYPE = 1;
            const int UP_EVENT_TYPE = 2;

            using var keyboardEvent = new KeyboardEvent
            {
                repeat = keyEvent.IsRepeat ? (byte)1 : (byte)0
            };

            using var key = new Keysym
            {
                sym = SDL2.WpfKeyToSdlKey(keyEvent),
            };
            keyboardEvent.keysym = key;
            
            if (keyEvent.IsDown)
            {
                keyboardEvent.type = DOWN_EVENT_TYPE;
                return _inputListeners.Any(inputListener => inputListener.keyPressed(keyboardEvent)) || _camera.keyPressed(keyboardEvent);
            }
            else if (keyEvent.IsUp)
            {
                 keyboardEvent.type = UP_EVENT_TYPE;
                 return _inputListeners.Any(inputListener => inputListener.keyReleased(keyboardEvent)) || _camera.keyReleased(keyboardEvent);
            }

            return false;
        }

        private bool WrapCursor(Point cursorPos)
        {
            var wrappedCursor = false;
            
            var left = (int)_owner.Left;
            var top = (int)_owner.Top;
            var right = (int)(_owner.Left + _owner.Width);
            var bottom = (int)(_owner.Top + _owner.Height);
            const int border = 5;
            
            if (cursorPos.X - border <= left)
            {
                OsNative.SetCursorPos(right - border * 2, (int)cursorPos.Y);
                wrappedCursor = true;
            }
            else if (cursorPos.X + border >= right)
            {
                OsNative.SetCursorPos(left + border * 2, (int)cursorPos.Y);
                wrappedCursor = true;
            }

            if (cursorPos.Y - border <= top)
            {
                OsNative.SetCursorPos((int)cursorPos.X, bottom - border * 2);
                wrappedCursor = true;
            }
            else if (cursorPos.Y + border >= bottom)
            {
                OsNative.SetCursorPos((int)cursorPos.X, top + border * 2);
                wrappedCursor = true;
            }

            return wrappedCursor;
        }

        public bool HandleMouseMove(Point mousePos, MouseEventArgs mouseEvent)
        {
            MakeMousePositionValid(mousePos);

            using var motionEvent = new MouseMotionEvent();
            motionEvent.x = (int)mousePos.X;
            motionEvent.y = (int)mousePos.Y;
            motionEvent.xrel = (int)(mousePos.X - _mousePrevPos.X);
            motionEvent.yrel = (int)(mousePos.Y - _mousePrevPos.Y);

            var eventConsumed = _inputListeners.Any(inputListener => inputListener.mouseMoved(motionEvent));

            if (mouseEvent.RightButton == MouseButtonState.Pressed || mouseEvent.LeftButton == MouseButtonState.Pressed)
            {
                if (Math.RealEqual(motionEvent.xrel, 0, float.Epsilon) ||
                    Math.RealEqual(motionEvent.yrel, 0, float.Epsilon))
                {
                    var screenPosition = _owner.PointToScreen(mousePos);
                    _mousePositionIsInvalid = WrapCursor(screenPosition);
                }
                
                if (!eventConsumed && (mouseEvent.RightButton == MouseButtonState.Pressed || (mouseEvent.LeftButton == MouseButtonState.Pressed && _canLeftClickToMoveCamera)))
                {
                    eventConsumed = _camera.mouseMoved(motionEvent);
                }
                
                OsNative.RestrictCursorToWindow(_owner);
            }
            else
            {
                OsNative.FreeCursor();
                _mousePositionIsInvalid = true;
            }
            
            _mousePrevPos = mousePos;
            
            return eventConsumed;
        }

        public bool HandleMouseInput(Point mousePos, MouseButtonEventArgs mouseEvent)
        {
            MakeMousePositionValid(mousePos);

            using var pressEvent = new MouseButtonEvent();
            pressEvent.clicks = (byte)mouseEvent.ClickCount;
            pressEvent.button = (byte)(mouseEvent.ChangedButton + 1);
            pressEvent.x = (int)mousePos.X;
            pressEvent.y = (int)mousePos.Y;
            switch (mouseEvent.ButtonState)
            {
                case MouseButtonState.Pressed:
                    return _inputListeners.Any(inputListener => inputListener.mousePressed(pressEvent)) || _camera.mousePressed(pressEvent);
                case MouseButtonState.Released:
                    mouseEvent.MouseDevice.OverrideCursor = null;
                    return _inputListeners.Any(inputListener => inputListener.mouseReleased(pressEvent)) || _camera.mouseReleased(pressEvent);
            }

            return false;
        }

        public bool HandleMouseWheel(Point mousePos, MouseWheelEventArgs mouseEvent)
        {
            MakeMousePositionValid(mousePos);

            using var wheelEvent = new MouseWheelEvent();
            wheelEvent.y = (int)(mouseEvent.Delta * 0.01);

            return _inputListeners.Any(inputListener => inputListener.mouseWheelRolled(wheelEvent)) || _camera.mouseWheelRolled(wheelEvent);
        }

        public void InitializeImGui(InputListener imGuiInputListener)
        {
            _inputListeners.Insert(0, imGuiInputListener);
        }

        public void CreateDefaultScene()
        {
            var root = _root;
            _sceneManager = root.createSceneManager();
            var shadergen = ShaderGenerator.getSingleton();
            shadergen.addSceneManager(_sceneManager); // must be done before we do anything with the scene
            
            _sceneManager.addRenderQueueListener(OverlaySystem.getSingleton());
            _sceneManager.setAmbientLight(new ColourValue(.1f, .1f, .1f));

            var light = _sceneManager.createLight("MainLight");
            light.setType(Light.LightTypes.LT_DIRECTIONAL);
            light.setDiffuseColour(new ColourValue(0.5f, 0.5f, 0.5f));
            light.setSpecularColour(new ColourValue(0.5f, 0.5f, 0.5f));
            var lightnode = _sceneManager.getRootSceneNode().createChildSceneNode();
            lightnode.attachObject(light);
            lightnode.setDirection(0, -1, 1);
            
            _mainCamera = _sceneManager.createCamera("MainCamera");
            _mainCamera.setAutoAspectRatio(true);
            _mainCamera.setNearClipDistance(1.0f);
            _mainCamera.setFarClipDistance(10000.0f);
            var camnode = _sceneManager.getRootSceneNode().createChildSceneNode("MainCameraNode");
            camnode.attachObject(_mainCamera);

            _camera = new CameraMan(camnode);
            SetCameraStyle(CameraStyle.CS_ORBIT);
            var quat = _camera.getCamera().getOrientation();
            _camera.setYawPitchDist(quat.getYaw(), quat.getPitch(), 20);

            var vp = _internalWindow.render.addViewport(_mainCamera);
            vp.setOverlaysEnabled(false);
            vp.setBackgroundColour(new ColourValue(.3f, .3f, .3f));
        }

        public void EnableImgui(bool enable)
        {
            _internalWindow.render.getViewport(0).setOverlaysEnabled(enable);
        }

        public Ray GetRayFromViewport(float x, float y)
        {
            var viewport = _internalWindow.render.getViewport(0);
            return _mainCamera.getCameraToViewportRay(x / viewport.getActualWidth(), y / viewport.getActualHeight());
        }

        public int GetViewportWidth()
        {
            return _internalWindow.render.getViewport(0).getActualWidth();
        }

        public int GetViewportHeight()
        {
            return _internalWindow.render.getViewport(0).getActualHeight();
        }

        public void ResetScene()
        {
            _camera.Dispose();
            _internalWindow.render.removeAllViewports();
            ShaderGenerator.getSingleton().removeSceneManager(_sceneManager);
            _sceneManager.Dispose();
            CreateDefaultScene();
        }

        public bool IsHidden()
        {
            return _internalWindow.render.isHidden();
        }

        public void SetHidden(bool isHidden)
        {
            if (_isClosed || isHidden == _internalWindow.render.isHidden())
            {
                return;
            }

            _internalWindow.render.setHidden(isHidden);
        }

        public Camera GetCamera()
        {
            return _mainCamera;
        }

        public void SetCameraTarget(SceneNode node)
        {
            _camera.setTarget(node);
            UpdateCamera();
        }

        public void UpdateCamera()
        {
            _camera.mousePressed(new MouseButtonEvent { button = ((int)MouseButton.Left + 1)});
            _camera.mouseMoved(new MouseMotionEvent());
        }

        public void SetCameraPosition(vec3 newPosition)
        {
            _sceneManager.getSceneNode("MainCameraNode").setPosition(newPosition.x, newPosition.y, newPosition.z);
            UpdateCamera();
        }

        public vec3 GetCameraPosition()
        {
            return OgreExtensions.FromOgre(_sceneManager.getSceneNode("MainCameraNode").getPosition());
        }

        public void EditScene(Action<OgreWindow> action)
        {
            action(this);
        }

        public void FrameUpdated(FrameEvent frameEvent)
        {
            _camera.frameRendered(frameEvent);
        }

        public void Render(float elapsedTime)
        {
            if (_isClosed)
            {
                return;
            }
            
            var overlaysEnabled = _internalWindow.render.getViewport(0).getOverlaysEnabled();
            if (overlaysEnabled)
            {
                ImGuiOverlay.NewFrame();
            }
            OnRender?.Invoke(this, EventArgs.Empty);
            if (overlaysEnabled)
            {
                ImGui.EndFrame();
            }
            
            _internalWindow.render.update(true);
        }

        public void NotifyWindowChanged()
        {
            _isResizing = true;
            _internalWindow.render.windowMovedOrResized();
            _isResizing = false;
        }

        public Root GetRoot() => _root;

        public SceneManager GetSceneManager() => _sceneManager;

        public void SetCameraStyle(CameraStyle style)
        {
            _camera.setStyle(style);
            _canLeftClickToMoveCamera = style == CameraStyle.CS_ORBIT;
        }

        public void SetYawPitchDist(float yaw, float pitch, float dist)
        {
            _camera.setYawPitchDist(new Radian(yaw), new Radian(pitch), dist);
        }

        public void SetCameraSpeed(float speed) => _camera.setTopSpeed(speed);

        public bool IsClosed() => _isClosed;

        public void Close()
        {
            if (_isClosed)
            {
                return;
            }
            
            _camera.Dispose();
            _internalWindow.render.setActive(false);
            var shadergen = ShaderGenerator.getSingleton();
            shadergen.removeSceneManager(_sceneManager);
            _sceneManager.Dispose();
            _internalWindow.Dispose();
            _isClosed = true;
        }

        private void MakeMousePositionValid(Point mousePos)
        {
            if (!_mousePositionIsInvalid)
            {
                return;
            }
            
            _mousePrevPos = mousePos;
            _mousePositionIsInvalid = false;
        }

        public bool IsResizing()
        {
            return _isResizing;
        }
    }
}
