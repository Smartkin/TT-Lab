using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace TT_Lab.Rendering.Input;

public class LabInputContext : IInputContext
{
    private readonly IView _view;
    private readonly Image _renderArea;

    public LabInputContext(IView view, Image renderArea)
    {
        _view = view;
        _renderArea = renderArea;
        
        Keyboards = [new LabKeyboard(renderArea)];
        Mice = [new LabMouse(renderArea)];
        
        _view.Update += ViewOnUpdate;
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
        ((LabMouse)Mice[0]).Dispose();
        ((LabKeyboard)Keyboards[0]).Dispose();
        GC.SuppressFinalize(this);
    }

    public IntPtr Handle => _view.Handle;
    public IReadOnlyList<IGamepad> Gamepads { get; } = [];
    public IReadOnlyList<IJoystick> Joysticks { get; } = [];
    public IReadOnlyList<IKeyboard> Keyboards { get; }
    public IReadOnlyList<IMouse> Mice { get; }
    public IReadOnlyList<IInputDevice> OtherDevices { get; } = [];
    public event Action<IInputDevice, Boolean>? ConnectionChanged;
}