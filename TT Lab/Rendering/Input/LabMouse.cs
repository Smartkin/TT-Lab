using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Controls;
using System.Windows.Input;
using Silk.NET.Input;
using MouseButton = Silk.NET.Input.MouseButton;

namespace TT_Lab.Rendering.Input;

public class LabMouse : IMouse, IDisposable
{
    private readonly Image _renderArea;
    private bool _scrollModified = false;
    
    public string Name => "TT Lab Mouse";
    public int Index => 0;
    public bool IsConnected => true;

    public LabMouse(Image renderArea)
    {
        Mouse.AddMouseMoveHandler(renderArea, MouseMoveHandler);
        Mouse.AddMouseDownHandler(renderArea, MouseDownHandler);
        Mouse.AddMouseUpHandler(renderArea, MouseUpHandler);
        Mouse.AddMouseWheelHandler(renderArea, MouseWheelHandler);
        Mouse.AddMouseEnterHandler(renderArea, MouseEnterHandler);
        
        _renderArea = renderArea;
    }

    public void Update()
    {
        if (!_scrollModified)
        {
            ((ScrollWheel[])ScrollWheels)[0] = default;
        }

        _scrollModified = false;
    }

    private void MouseEnterHandler(object sender, MouseEventArgs e)
    {
        Keyboard.Focus(_renderArea);
        _renderArea.Focus();
    }

    private void MouseWheelHandler(object sender, MouseWheelEventArgs e)
    {
        var scrollWheel = new ScrollWheel(0, (float)e.Delta / Mouse.MouseWheelDeltaForOneLine);
        if (Math.Abs(ScrollWheels[0].Y - scrollWheel.Y) > 0.001f)
        {
            _scrollModified = true;
        }
        ((ScrollWheel[])ScrollWheels)[0] = scrollWheel;
        
        Scroll?.Invoke(this, scrollWheel);
    }

    private void MouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        var button = GetButton(e.ChangedButton);
        if (button == null)
        {
            return;
        }
        
        MouseUp?.Invoke(this, button.Value);
    }

    private void MouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        var button = GetButton(e.ChangedButton);
        if (button == null)
        {
            return;
        }
        
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
    
    private void MouseMoveHandler(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(_renderArea);
        Position = new Vector2((float)position.X, (float)position.Y);
        MouseMove?.Invoke(this, Position);
    }

    public void Dispose()
    {
        Mouse.RemoveMouseMoveHandler(_renderArea, MouseMoveHandler);
        Mouse.RemoveMouseDownHandler(_renderArea, MouseDownHandler);
        Mouse.RemoveMouseUpHandler(_renderArea, MouseUpHandler);
        Mouse.RemoveMouseWheelHandler(_renderArea, MouseWheelHandler);
        Mouse.RemoveMouseEnterHandler(_renderArea, MouseEnterHandler);
        
        ((LabCursor)Cursor).Dispose();
        GC.SuppressFinalize(this);
    }

    public Boolean IsButtonPressed(MouseButton btn)
    {
        return btn switch
        {
            MouseButton.Left => Mouse.LeftButton == MouseButtonState.Pressed,
            MouseButton.Right => Mouse.RightButton == MouseButtonState.Pressed,
            MouseButton.Middle => Mouse.MiddleButton == MouseButtonState.Pressed,
            MouseButton.Button4 => Mouse.XButton1 == MouseButtonState.Pressed,
            MouseButton.Button5 => Mouse.XButton2 == MouseButtonState.Pressed,
            _ => false
        };
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

    public static MouseButton? GetButton(System.Windows.Input.MouseButton btn)
    {
        return btn switch
        {
            System.Windows.Input.MouseButton.Left => MouseButton.Left,
            System.Windows.Input.MouseButton.Middle => MouseButton.Middle,
            System.Windows.Input.MouseButton.Right => MouseButton.Right,
            System.Windows.Input.MouseButton.XButton1 => MouseButton.Button4,
            System.Windows.Input.MouseButton.XButton2 => MouseButton.Button5,
            _ => null
        };
    }
}