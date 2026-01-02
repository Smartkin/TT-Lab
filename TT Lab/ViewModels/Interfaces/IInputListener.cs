using System;
using System.Windows.Input;
using Avalonia.Input;

namespace TT_Lab.ViewModels.Interfaces;

/// <summary>
/// Allows listening to keyboard and mouse inputs. Return true if the input is consumed to not propagate it further
/// </summary>
public interface IInputListener
{
    bool MouseMove(Object? sender, PointerEventArgs e) { return false; }
    bool MouseDown(Object? sender, PointerPressedEventArgs e) { return false; }
    bool MouseUp(Object? sender, PointerPressedEventArgs e) { return false; }
    bool MouseWheel(Object? sender, PointerWheelEventArgs e) { return false; }
    bool KeyPressed(Object sender, KeyEventArgs arg) { return false; }
    bool KeyReleased(Object sender, KeyEventArgs arg) { return false; }
}