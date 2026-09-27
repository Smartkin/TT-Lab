using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Silk.NET.Input;
using TT_Lab.Controls;
using MouseButton = Silk.NET.Input.MouseButton;

namespace TT_Lab.Rendering.Input;

public class LabMouse : IMouse, IDisposable
{
    private Viewport? _renderArea;
    private bool _scrollModified = false;
    private Pointer _mouse;
    private Dictionary<MouseButton, bool> _pressedMouseButtons = new();
    
    public string Name => "TT Lab Avalonia Mouse";
    public int Index => 0;
    public bool IsConnected => true;

    public LabMouse(Viewport? renderArea)
    {
        _mouse = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        Attach(renderArea);
    }

    // The viewport's input comes from whichever control shows it, buttons let go of while none did would stay pressed
    public void Attach(Viewport? renderArea)
    {
        if (_renderArea != null)
        {
            _renderArea.PointerEntered      -= MouseEnterHandler;
            _renderArea.PointerMoved        -= MouseMoveHandler;
            _renderArea.PointerPressed      -= MouseDownHandler;
            _renderArea.PointerReleased     -= MouseUpHandler;
            _renderArea.PointerWheelChanged -= MouseWheelHandler;
        }

        _pressedMouseButtons.Clear();
        _renderArea = renderArea;
        _mouse.Capture(renderArea);
        if (renderArea == null)
        {
            return;
        }

        renderArea.PointerEntered      += MouseEnterHandler;
        renderArea.PointerMoved        += MouseMoveHandler;
        renderArea.PointerPressed      += MouseDownHandler;
        renderArea.PointerReleased     += MouseUpHandler;
        renderArea.PointerWheelChanged += MouseWheelHandler;
    }

    public void Update()
    {
        if (!_scrollModified)
        {
            ((ScrollWheel[])ScrollWheels)[0] = default;
        }

        _scrollModified = false;
    }

    private void MouseEnterHandler(object? sender, PointerEventArgs e)
    {
        _mouse.Capture(_renderArea);
    }

    private void MouseWheelHandler(object? sender, PointerWheelEventArgs e)
    {
        var scrollWheel = new ScrollWheel((float)e.Delta.X, (float)e.Delta.Y);
        if (Math.Abs(ScrollWheels[0].Y - scrollWheel.Y) > 0.001f)
        {
            _scrollModified = true;
        }
        ((ScrollWheel[])ScrollWheels)[0] = scrollWheel;
        
        Scroll?.Invoke(this, scrollWheel);
    }

    private void MouseUpHandler(object? sender, PointerReleasedEventArgs e)
    {
        var button = GetButton(e.InitialPressMouseButton);
        if (button == null)
        {
            return;
        }

        _pressedMouseButtons[button.Value] = false;
        UpdatePosition(e);
        
        MouseUp?.Invoke(this, button.Value);
    }

    private void MouseDownHandler(object? sender, PointerPressedEventArgs e)
    {
        var button = GetButton(e.Properties);
        if (button == null)
        {
            return;
        }

        _pressedMouseButtons[button.Value] = true;
        // Taking the focus on hover used to pull it out of the inspector, keys meant for a text field then edited the scene
        _renderArea?.Focus(NavigationMethod.Pointer);
        // Presses pick what's under the mouse, which may have moved without a move event before it
        UpdatePosition(e);
        
        MouseDown?.Invoke(this, button.Value);

        switch (e.ClickCount)
        {
            case 1:
            {
                var position = e.GetPosition(_renderArea);
                Click?.Invoke(this, button.Value, new Vector2((float)position.X, (float)position.Y));
                break;
            }
            case 2:
            {
                var position = e.GetPosition(_renderArea);
                DoubleClick?.Invoke(this, button.Value, new Vector2((float)position.X, (float)position.Y));
                break;
            }
        }
    }
    
    private void MouseMoveHandler(object? sender, PointerEventArgs e)
    {
        UpdatePosition(e);
        MouseMove?.Invoke(this, Position);
    }

    private void UpdatePosition(PointerEventArgs e)
    {
        var position = e.GetPosition(_renderArea);
        Position = new Vector2((float)position.X, (float)position.Y);
    }

    public void Dispose()
    {
        Attach(null);
        ((LabCursor)Cursor).Dispose();
        GC.SuppressFinalize(this);
    }

    public Boolean IsButtonPressed(MouseButton btn)
    {
        return _pressedMouseButtons.ContainsKey(btn) && _pressedMouseButtons[btn];
    }

    public IReadOnlyList<MouseButton> SupportedButtons { get; } = [MouseButton.Left, MouseButton.Middle, MouseButton.Right, MouseButton.Button4, MouseButton.Button5];
    public IReadOnlyList<ScrollWheel> ScrollWheels { get; } = new ScrollWheel[1];
    public Vector2 Position { get; set; }
    public ICursor Cursor { get; } = new LabCursor();
    public int DoubleClickTime { get; set; }
    public int DoubleClickRange { get; set; }
    public event Action<IMouse, MouseButton>? MouseDown;
    public event Action<IMouse, MouseButton>? MouseUp;
    public event Action<IMouse, MouseButton, Vector2>? Click;
    public event Action<IMouse, MouseButton, Vector2>? DoubleClick;
    public event Action<IMouse, Vector2>? MouseMove;
    public event Action<IMouse, ScrollWheel>? Scroll;
    
    private static MouseButton? GetButton(PointerPointProperties btn)
    {
        if (btn.IsLeftButtonPressed)
        {
            return MouseButton.Left;
        }
        if (btn.IsMiddleButtonPressed)
        {
            return MouseButton.Middle;
        }
        if (btn.IsRightButtonPressed)
        {
            return MouseButton.Right;
        }
        if (btn.IsXButton1Pressed)
        {
            return MouseButton.Button4;
        }
        if (btn.IsXButton2Pressed)
        {
            return MouseButton.Button5;
        }

        return null;
    }

    private static MouseButton? GetButton(Avalonia.Input.MouseButton btn)
    {
        return btn switch
        {
            Avalonia.Input.MouseButton.Left => MouseButton.Left,
            Avalonia.Input.MouseButton.Middle => MouseButton.Middle,
            Avalonia.Input.MouseButton.Right => MouseButton.Right,
            Avalonia.Input.MouseButton.XButton1 => MouseButton.Button4,
            Avalonia.Input.MouseButton.XButton2 => MouseButton.Button5,
            _ => null
        };
    }
}