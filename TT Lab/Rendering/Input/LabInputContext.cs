using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Silk.NET.Input;
using Silk.NET.Windowing;
using TT_Lab.Controls;

namespace TT_Lab.Rendering.Input;

public class LabInputContext : IInputContext
{
    private readonly ViewportHost _host;
    private IView? _view;

    public LabInputContext(ViewportHost host)
    {
        _host = host;
        
        Keyboards = [new LabKeyboard(host.Presenter)];
        Mice = [new LabMouse(host.Presenter)];
        
        _host.PresenterChanged += HostOnPresenterChanged;
    }

    // The renderer gets made on the render thread, the input has to be hooked to the controls on the UI thread
    public void SetView(IView view)
    {
        _view = view;
        _view.Update += ViewOnUpdate;
    }

    private void HostOnPresenterChanged(Viewport? presenter)
    {
        ((LabMouse)Mice[0]).Attach(presenter);
        ((LabKeyboard)Keyboards[0]).Attach(presenter);
    }

    private void ViewOnUpdate(double obj)
    {
        foreach (var mouse in Mice.Cast<LabMouse>())
        {
            mouse.Update();
        }
    }

    public void Dispose()
    {
        _host.PresenterChanged -= HostOnPresenterChanged;
        if (_view != null)
        {
            _view.Update -= ViewOnUpdate;
        }

        ((LabMouse)Mice[0]).Dispose();
        ((LabKeyboard)Keyboards[0]).Dispose();
        GC.SuppressFinalize(this);
    }

    public IntPtr Handle => _view?.Handle ?? IntPtr.Zero;
    public IReadOnlyList<IGamepad> Gamepads { get; } = [];
    public IReadOnlyList<IJoystick> Joysticks { get; } = [];
    public IReadOnlyList<IKeyboard> Keyboards { get; }
    public IReadOnlyList<IMouse> Mice { get; }
    public IReadOnlyList<IInputDevice> OtherDevices { get; } = [];
    public event Action<IInputDevice, Boolean>? ConnectionChanged;
}