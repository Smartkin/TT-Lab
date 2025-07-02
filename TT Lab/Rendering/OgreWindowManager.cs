using org.ogre;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.Util;
using Math = org.ogre.Math;

namespace TT_Lab.Rendering
{
    /// <summary>
    /// Manages all the windows that are rendered to and creates new ones on request
    /// </summary>
    public class OgreWindowManager : ApplicationContextBase
    {
        private NativeWindowPair _internalWindow;
        private readonly List<OgreWindow> _ogreWindows = new();
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private bool _isResizing = false;
        private Stopwatch _renderWatch = new();
        
        public object RenderLockObject = new();

        public OgreWindowManager() { }

        /// <summary>
        /// Initializes the manager as well as the rendering system
        /// </summary>
        public void Initialize()
        {
            Debug.Assert(!_isInitialized, "Can only initialize the OGRE window manager once");
            
            createRoot();
            if (!getRoot().restoreConfig())
            {
                getRoot().showConfigDialog(null);
            }
            setup();

            // ResourceGroupManager.getSingleton().createResourceGroup(GlobalConsts.OgreGroup, true);
            // ResourceGroupManager.getSingleton().addResourceLocation($"{ManifestResourceLoader.GetPathInExe("")}/Media", "FileSystem", GlobalConsts.OgreGroup, true);
            // ResourceGroupManager.getSingleton().initialiseResourceGroup(GlobalConsts.OgreGroup);
            // ResourceGroupManager.getSingleton().loadResourceGroup(GlobalConsts.OgreGroup);
            MaterialManager.Initialize();

            _isInitialized = true;
        }

        /// <summary>
        /// Creates new rendering surface in the designated window with a default scene
        /// </summary>
        /// <param name="windowHandle"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <returns></returns>
        public OgreWindow CreateWindow(IntPtr windowHandle, UInt32 w, UInt32 h)
        {
            Debug.Assert(_isInitialized, "Must initialize the rendering system before creating any rendering windows");

            var miscParams = new NameValueMap
            {
                ["externalWindowHandle"] = windowHandle.ToString()
            };

            var ogreWindow = new OgreWindow(getRoot(), windowHandle);
            var windowName = $"EMBEDDED WINDOW {ogreWindow.GetHashCode()}";
            var newWindow = createWindow(windowName, w, h, miscParams);
            newWindow.render.setAutoUpdated(false);
            ogreWindow.SetInternalWindow(newWindow);
            ogreWindow.CreateDefaultScene();
            ogreWindow.InitializeImGui(getImGuiInputListener());
            _ogreWindows.Add(ogreWindow);

            return ogreWindow;
        }

        public override void loadResources()
        {
            setRTSSWriteShadersToDisk(true);
            base.loadResources();
        }

        public override void locateResources()
        {
            ResourceGroupManager.getSingleton().createResourceGroup(GlobalConsts.OgreGroup, true);
            base.locateResources();
        }

        /// <summary>
        /// DO NOT USE DIRECTLY THIS ONE IS FOR INTERNAL USE ONLY. USE <see cref="CreateWindow(nint, uint, uint)"/> INSTEAD
        /// </summary>
        /// <param name="name"></param>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="miscParams"></param>
        /// <returns></returns>
        public override NativeWindowPair createWindow(String name, UInt32 w, UInt32 h, NameValueMap miscParams)
        {
            if (_isInitialized)
            {
                return base.createWindow(name, w, h, miscParams);
            }

            name = "MANAGER WINDOW";
            w = 1;
            h = 1;
            miscParams["externalWindowHandle"] = IntPtr.Zero.ToString();
            miscParams["hidden"] = "true";
            _internalWindow = base.createWindow(name, w, h, miscParams);
            
            initialiseImGui();
            var imGuiOverlay = OverlayManager.getSingleton().getByName("ImGuiOverlay");
            imGuiOverlay.setZOrder(300);
            imGuiOverlay.show();
            
            return _internalWindow;
        }

        public void AddResourceLocation(string location)
        {
            // ResourceGroupManager.getSingleton().createResourceGroup(GlobalConsts.OgreGroup);
            // ResourceGroupManager.getSingleton().addResourceLocation(location, "FileSystem", GlobalConsts.OgreGroup, true);
        }

        public void RemoveResourceLocation(string location)
        {
            // ResourceGroupManager.getSingleton().removeResourceLocation(location, GlobalConsts.OgreGroup);
        }

        /// <summary>
        /// Makes all currently existing rendering surfaces to do one full render cycle
        /// </summary>
        public void Render()
        {
            if (_ogreWindows.Count == 0 || _isDisposed || _isResizing || _ogreWindows.Any(w => w.IsResizing()))
            {
                return;
            }
            
            var elapsed =  System.Math.Max(_renderWatch.ElapsedMilliseconds / 1000.0f, 1.0f / 60.0f);
            if (Math.RealEqual(elapsed, 0.0f, float.Epsilon))
            {
                elapsed = 1.0f / 60.0f;
            }

            FrameEvent evt = new()
            {
                timeSinceLastEvent = elapsed,
                timeSinceLastFrame = elapsed,
            };

            if (Root.getCPtr(getRoot()).Handle == IntPtr.Zero || !getRoot().isInitialised())
            {
                return;
            }
            
            getRoot().renderOneFrame(elapsed);
            foreach (var window in _ogreWindows.Where(window => !window.IsHidden()))
            {
                window.Render(elapsed);
                window.FrameUpdated(evt);
            }
            
            _renderWatch.Restart();
        }

        public override Boolean frameRenderingQueued(FrameEvent evt)
        {
            foreach (var window in _ogreWindows.Where(window => !window.IsHidden()))
            {
                window.FrameUpdated(evt);
            }
            
            return base.frameRenderingQueued(evt);
        }

        /// <summary>
        /// Notifies all internal rendering surfaces that the controlling window was resized or repositioned
        /// </summary>
        public void NotifyResizeAllWindows()
        {
            lock (RenderLockObject)
            {
                _isResizing = true;
                foreach (var window in _ogreWindows)
                {
                    window.NotifyWindowChanged();
                }
                _isResizing = false;
            }
        }

        /// <summary>
        /// Closes and frees all resources from the given rendering surface
        /// </summary>
        /// <param name="window"></param>
        public void CloseWindow(OgreWindow window)
        {
            _ogreWindows.Remove(window);
            window.Close();
        }

        /// <summary>
        /// Terminates everything and the renderer
        /// </summary>
        public void CloseAndTerminateAll()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (RenderLockObject)
            {
                _isDisposed = true;
                closeApp();
                Dispose();
            }
        }
    }
}
