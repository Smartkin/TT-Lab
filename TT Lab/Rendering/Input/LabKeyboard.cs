using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Silk.NET.Input;
using TT_Lab.Controls;
using Key = Silk.NET.Input.Key;

namespace TT_Lab.Rendering.Input;

public class LabKeyboard : IKeyboard, IDisposable
{
    private static readonly Key[] Keys = Enum.GetValues<Avalonia.Input.Key>().Select(ConvertKey)
        .Where(static x => x != Key.Unknown).Distinct().ToArray();

    private readonly Viewport _renderArea;
    private readonly Dictionary<Key, bool> _keysPressed = new();
    private readonly Dictionary<int, bool> _scancodesPressed = new();
    
    public string Name => "TT Lab Avalonia Keyboard";
    public int Index => 0;
    public bool IsConnected => true;

    public LabKeyboard(Viewport renderArea)
    {
        renderArea.KeyDown += KeyDownHandler;
        renderArea.KeyUp += KeyUpHandler;
        // InputElement.KeyDownEvent.Raised.Subscribe(this);
        // Keyboard.AddKeyDownHandler(renderArea, KeyDownHandler);
        // Keyboard.AddKeyUpHandler(renderArea, KeyUpHandler);
        _renderArea = renderArea;
    }

    public void Dispose()
    {
        _renderArea.KeyDown -= KeyDownHandler;
        _renderArea.KeyUp -= KeyUpHandler;
        // Keyboard.RemoveKeyDownHandler(_renderArea, KeyDownHandler);
        // Keyboard.RemoveKeyUpHandler(_renderArea, KeyUpHandler);
        GC.SuppressFinalize(this);
    }

    private void KeyUpHandler(object? sender, KeyEventArgs e)
    {
        var key = ConvertKey(e.Key);
        _keysPressed[key] = false;
        _scancodesPressed[(int)e.PhysicalKey] = false;
        KeyUp?.Invoke(this, key, (int)e.PhysicalKey);
        e.Handled = true;
    }

    private void KeyDownHandler(object? sender, KeyEventArgs e)
    {
        var key = ConvertKey(e.Key);
        _keysPressed[key] = true;
        _scancodesPressed[(int)e.PhysicalKey] = true;
        KeyDown?.Invoke(this, key, (int)e.PhysicalKey);
        if (e.KeySymbol != null)
        {
            KeyChar?.Invoke(this, e.KeySymbol.ToCharArray()[0]);
        }

        e.Handled = true;
    }

    public Boolean IsKeyPressed(Key key) => _keysPressed.ContainsKey(key) && _keysPressed[key];

    public Boolean IsScancodePressed(int scancode) => _scancodesPressed.ContainsKey(scancode) && _scancodesPressed[scancode];

    public void BeginInput()
    {
    }

    public void EndInput()
    {
    }

    public IReadOnlyList<Key> SupportedKeys => Keys;

    public string ClipboardText
    {
        get => ""; //Clipboard.GetText();
        set {} //Clipboard.SetText(value);
    }

    public event Action<IKeyboard, Key, Int32>? KeyDown;
    public event Action<IKeyboard, Key, Int32>? KeyUp;
    public event Action<IKeyboard, Char>? KeyChar;

    private static char KeyToChar(Avalonia.Input.Key key)
    {
        // if (Keyboard.IsKeyDown(Avalonia.Input.Key.LeftAlt) ||
        //     Keyboard.IsKeyDown(Avalonia.Input.Key.RightAlt) ||
        //     Keyboard.IsKeyDown(Avalonia.Input.Key.LeftCtrl) ||
        //     Keyboard.IsKeyDown(Avalonia.Input.Key.RightAlt))
        // {
        //     return '\0';
        // }

        var caplock = Console.CapsLock;
        var shift = false;
        // Keyboard.IsKeyDown(Avalonia.Input.Key.LeftShift) ||
        //              Keyboard.IsKeyDown(Avalonia.Input.Key.RightShift);
        var iscap = (caplock && !shift) || (!caplock && shift);
        return key switch
        {
            Avalonia.Input.Key.Enter => '\n',
            Avalonia.Input.Key.A => (iscap ? 'A' : 'a'),
            Avalonia.Input.Key.B => (iscap ? 'B' : 'b'),
            Avalonia.Input.Key.C => (iscap ? 'C' : 'c'),
            Avalonia.Input.Key.D => (iscap ? 'D' : 'd'),
            Avalonia.Input.Key.E => (iscap ? 'E' : 'e'),
            Avalonia.Input.Key.F => (iscap ? 'F' : 'f'),
            Avalonia.Input.Key.G => (iscap ? 'G' : 'g'),
            Avalonia.Input.Key.H => (iscap ? 'H' : 'h'),
            Avalonia.Input.Key.I => (iscap ? 'I' : 'i'),
            Avalonia.Input.Key.J => (iscap ? 'J' : 'j'),
            Avalonia.Input.Key.K => (iscap ? 'K' : 'k'),
            Avalonia.Input.Key.L => (iscap ? 'L' : 'l'),
            Avalonia.Input.Key.M => (iscap ? 'M' : 'm'),
            Avalonia.Input.Key.N => (iscap ? 'N' : 'n'),
            Avalonia.Input.Key.O => (iscap ? 'O' : 'o'),
            Avalonia.Input.Key.P => (iscap ? 'P' : 'p'),
            Avalonia.Input.Key.Q => (iscap ? 'Q' : 'q'),
            Avalonia.Input.Key.R => (iscap ? 'R' : 'r'),
            Avalonia.Input.Key.S => (iscap ? 'S' : 's'),
            Avalonia.Input.Key.T => (iscap ? 'T' : 't'),
            Avalonia.Input.Key.U => (iscap ? 'U' : 'u'),
            Avalonia.Input.Key.V => (iscap ? 'V' : 'v'),
            Avalonia.Input.Key.W => (iscap ? 'W' : 'w'),
            Avalonia.Input.Key.X => (iscap ? 'X' : 'x'),
            Avalonia.Input.Key.Y => (iscap ? 'Y' : 'y'),
            Avalonia.Input.Key.Z => (iscap ? 'Z' : 'z'),
            Avalonia.Input.Key.D0 => (shift ? ')' : '0'),
            Avalonia.Input.Key.D1 => (shift ? '!' : '1'),
            Avalonia.Input.Key.D2 => (shift ? '@' : '2'),
            Avalonia.Input.Key.D3 => (shift ? '#' : '3'),
            Avalonia.Input.Key.D4 => (shift ? '$' : '4'),
            Avalonia.Input.Key.D5 => (shift ? '%' : '5'),
            Avalonia.Input.Key.D6 => (shift ? '^' : '6'),
            Avalonia.Input.Key.D7 => (shift ? '&' : '7'),
            Avalonia.Input.Key.D8 => (shift ? '*' : '8'),
            Avalonia.Input.Key.D9 => (shift ? '(' : '9'),
            Avalonia.Input.Key.OemPlus => (shift ? '+' : '='),
            Avalonia.Input.Key.OemMinus => (shift ? '_' : '-'),
            Avalonia.Input.Key.OemQuestion => (shift ? '?' : '/'),
            Avalonia.Input.Key.OemComma => (shift ? '<' : ','),
            Avalonia.Input.Key.OemPeriod => (shift ? '>' : '.'),
            Avalonia.Input.Key.OemOpenBrackets => (shift ? '{' : '['),
            Avalonia.Input.Key.OemQuotes => (shift ? '"' : '\''),
            Avalonia.Input.Key.Oem1 => (shift ? ':' : ';'),
            Avalonia.Input.Key.Oem3 => (shift ? '~' : '`'),
            Avalonia.Input.Key.Oem5 => (shift ? '|' : '\\'),
            Avalonia.Input.Key.Oem6 => (shift ? '}' : ']'),
            Avalonia.Input.Key.Tab => '\t',
            Avalonia.Input.Key.Space => ' ',
            // Number Pad
            Avalonia.Input.Key.NumPad0 => '0',
            Avalonia.Input.Key.NumPad1 => '1',
            Avalonia.Input.Key.NumPad2 => '2',
            Avalonia.Input.Key.NumPad3 => '3',
            Avalonia.Input.Key.NumPad4 => '4',
            Avalonia.Input.Key.NumPad5 => '5',
            Avalonia.Input.Key.NumPad6 => '6',
            Avalonia.Input.Key.NumPad7 => '7',
            Avalonia.Input.Key.NumPad8 => '8',
            Avalonia.Input.Key.NumPad9 => '9',
            Avalonia.Input.Key.Subtract => '-',
            Avalonia.Input.Key.Add => '+',
            Avalonia.Input.Key.Decimal => '.',
            Avalonia.Input.Key.Divide => '/',
            Avalonia.Input.Key.Multiply => '*',
            _ => '\0'
        };
    }

    private static Key ConvertKey(Avalonia.Input.Key keys) =>
        keys switch
        {
            Avalonia.Input.Key.None => Key.Unknown,
            Avalonia.Input.Key.Space => Key.Space,
            Avalonia.Input.Key.OemComma => Key.Comma,
            Avalonia.Input.Key.OemMinus => Key.Minus,
            Avalonia.Input.Key.OemPeriod => Key.Period,
            Avalonia.Input.Key.D0 => Key.Number0,
            Avalonia.Input.Key.D1 => Key.Number1,
            Avalonia.Input.Key.D2 => Key.Number2,
            Avalonia.Input.Key.D3 => Key.Number3,
            Avalonia.Input.Key.D4 => Key.Number4,
            Avalonia.Input.Key.D5 => Key.Number5,
            Avalonia.Input.Key.D6 => Key.Number6,
            Avalonia.Input.Key.D7 => Key.Number7,
            Avalonia.Input.Key.D8 => Key.Number8,
            Avalonia.Input.Key.D9 => Key.Number9,
            Avalonia.Input.Key.OemSemicolon => Key.Semicolon,
            Avalonia.Input.Key.A => Key.A,
            Avalonia.Input.Key.B => Key.B,
            Avalonia.Input.Key.C => Key.C,
            Avalonia.Input.Key.D => Key.D,
            Avalonia.Input.Key.E => Key.E,
            Avalonia.Input.Key.F => Key.F,
            Avalonia.Input.Key.G => Key.G,
            Avalonia.Input.Key.H => Key.H,
            Avalonia.Input.Key.I => Key.I,
            Avalonia.Input.Key.J => Key.J,
            Avalonia.Input.Key.K => Key.K,
            Avalonia.Input.Key.L => Key.L,
            Avalonia.Input.Key.M => Key.M,
            Avalonia.Input.Key.N => Key.N,
            Avalonia.Input.Key.O => Key.O,
            Avalonia.Input.Key.P => Key.P,
            Avalonia.Input.Key.Q => Key.Q,
            Avalonia.Input.Key.R => Key.R,
            Avalonia.Input.Key.S => Key.S,
            Avalonia.Input.Key.T => Key.T,
            Avalonia.Input.Key.U => Key.U,
            Avalonia.Input.Key.V => Key.V,
            Avalonia.Input.Key.W => Key.W,
            Avalonia.Input.Key.X => Key.X,
            Avalonia.Input.Key.Y => Key.Y,
            Avalonia.Input.Key.Z => Key.Z,
            Avalonia.Input.Key.OemOpenBrackets => Key.LeftBracket,
            Avalonia.Input.Key.OemBackslash => Key.BackSlash,
            Avalonia.Input.Key.OemCloseBrackets => Key.RightBracket,
            Avalonia.Input.Key.Escape => Key.Escape,
            Avalonia.Input.Key.Enter => Key.Enter,
            Avalonia.Input.Key.Tab => Key.Tab,
            Avalonia.Input.Key.Back => Key.Backspace,
            Avalonia.Input.Key.Insert => Key.Insert,
            Avalonia.Input.Key.Delete => Key.Delete,
            Avalonia.Input.Key.Right => Key.Right,
            Avalonia.Input.Key.Left => Key.Left,
            Avalonia.Input.Key.Down => Key.Down,
            Avalonia.Input.Key.Up => Key.Up,
            Avalonia.Input.Key.PageUp => Key.PageUp,
            Avalonia.Input.Key.PageDown => Key.PageDown,
            Avalonia.Input.Key.Home => Key.Home,
            Avalonia.Input.Key.End => Key.End,
            Avalonia.Input.Key.CapsLock => Key.CapsLock,
            Avalonia.Input.Key.Scroll => Key.ScrollLock,
            Avalonia.Input.Key.NumLock => Key.NumLock,
            Avalonia.Input.Key.PrintScreen => Key.PrintScreen,
            Avalonia.Input.Key.Pause => Key.Pause,
            Avalonia.Input.Key.F1 => Key.F1,
            Avalonia.Input.Key.F2 => Key.F2,
            Avalonia.Input.Key.F3 => Key.F3,
            Avalonia.Input.Key.F4 => Key.F4,
            Avalonia.Input.Key.F5 => Key.F5,
            Avalonia.Input.Key.F6 => Key.F6,
            Avalonia.Input.Key.F7 => Key.F7,
            Avalonia.Input.Key.F8 => Key.F8,
            Avalonia.Input.Key.F9 => Key.F9,
            Avalonia.Input.Key.F10 => Key.F10,
            Avalonia.Input.Key.F11 => Key.F11,
            Avalonia.Input.Key.F12 => Key.F12,
            Avalonia.Input.Key.F13 => Key.F13,
            Avalonia.Input.Key.F14 => Key.F14,
            Avalonia.Input.Key.F15 => Key.F15,
            Avalonia.Input.Key.F16 => Key.F16,
            Avalonia.Input.Key.F17 => Key.F17,
            Avalonia.Input.Key.F18 => Key.F18,
            Avalonia.Input.Key.F19 => Key.F19,
            Avalonia.Input.Key.F20 => Key.F20,
            Avalonia.Input.Key.F21 => Key.F21,
            Avalonia.Input.Key.F22 => Key.F22,
            Avalonia.Input.Key.F23 => Key.F23,
            Avalonia.Input.Key.F24 => Key.F24,
            Avalonia.Input.Key.NumPad0 => Key.Keypad0,
            Avalonia.Input.Key.NumPad1 => Key.Keypad1,
            Avalonia.Input.Key.NumPad2 => Key.Keypad2,
            Avalonia.Input.Key.NumPad3 => Key.Keypad3,
            Avalonia.Input.Key.NumPad4 => Key.Keypad4,
            Avalonia.Input.Key.NumPad5 => Key.Keypad5,
            Avalonia.Input.Key.NumPad6 => Key.Keypad6,
            Avalonia.Input.Key.NumPad7 => Key.Keypad7,
            Avalonia.Input.Key.NumPad8 => Key.Keypad8,
            Avalonia.Input.Key.NumPad9 => Key.Keypad9,
            Avalonia.Input.Key.Decimal => Key.KeypadDecimal,
            Avalonia.Input.Key.Divide => Key.KeypadDivide,
            Avalonia.Input.Key.Multiply => Key.KeypadMultiply,
            Avalonia.Input.Key.Subtract => Key.KeypadSubtract,
            Avalonia.Input.Key.Add => Key.KeypadAdd,
            Avalonia.Input.Key.LeftShift => Key.ShiftLeft,
            Avalonia.Input.Key.LeftCtrl => Key.ControlLeft,
            Avalonia.Input.Key.LeftAlt => Key.AltLeft,
            Avalonia.Input.Key.LWin => Key.SuperLeft,
            Avalonia.Input.Key.RightShift => Key.ShiftRight,
            Avalonia.Input.Key.RightCtrl => Key.ControlRight,
            Avalonia.Input.Key.RightAlt => Key.AltRight,
            Avalonia.Input.Key.RWin => Key.SuperRight,
            _ => Key.Unknown
        };

    private static Avalonia.Input.Key ConvertKey(Key keys) =>
        keys switch
        {
            Key.Unknown => Avalonia.Input.Key.None,
            Key.Space => Avalonia.Input.Key.Space,
            Key.Comma => Avalonia.Input.Key.OemComma,
            Key.Minus => Avalonia.Input.Key.OemMinus,
            Key.Period => Avalonia.Input.Key.Decimal,
            Key.Slash => Avalonia.Input.Key.Divide,
            Key.Number0 => Avalonia.Input.Key.D0,
            Key.Number1 => Avalonia.Input.Key.D1,
            Key.Number2 => Avalonia.Input.Key.D2,
            Key.Number3 => Avalonia.Input.Key.D3,
            Key.Number4 => Avalonia.Input.Key.D4,
            Key.Number5 => Avalonia.Input.Key.D5,
            Key.Number6 => Avalonia.Input.Key.D6,
            Key.Number7 => Avalonia.Input.Key.D7,
            Key.Number8 => Avalonia.Input.Key.D8,
            Key.Number9 => Avalonia.Input.Key.D9,
            Key.Semicolon => Avalonia.Input.Key.OemSemicolon,
            Key.A => Avalonia.Input.Key.A,
            Key.B => Avalonia.Input.Key.B,
            Key.C => Avalonia.Input.Key.C,
            Key.D => Avalonia.Input.Key.D,
            Key.E => Avalonia.Input.Key.E,
            Key.F => Avalonia.Input.Key.F,
            Key.G => Avalonia.Input.Key.G,
            Key.H => Avalonia.Input.Key.H,
            Key.I => Avalonia.Input.Key.I,
            Key.J => Avalonia.Input.Key.J,
            Key.K => Avalonia.Input.Key.K,
            Key.L => Avalonia.Input.Key.L,
            Key.M => Avalonia.Input.Key.M,
            Key.N => Avalonia.Input.Key.N,
            Key.O => Avalonia.Input.Key.O,
            Key.P => Avalonia.Input.Key.P,
            Key.Q => Avalonia.Input.Key.Q,
            Key.R => Avalonia.Input.Key.R,
            Key.S => Avalonia.Input.Key.S,
            Key.T => Avalonia.Input.Key.T,
            Key.U => Avalonia.Input.Key.U,
            Key.V => Avalonia.Input.Key.V,
            Key.W => Avalonia.Input.Key.W,
            Key.X => Avalonia.Input.Key.X,
            Key.Y => Avalonia.Input.Key.Y,
            Key.Z => Avalonia.Input.Key.Z,
            Key.Escape => Avalonia.Input.Key.Escape,
            Key.Enter => Avalonia.Input.Key.Enter,
            Key.Tab => Avalonia.Input.Key.Tab,
            Key.Backspace => Avalonia.Input.Key.Back,
            Key.Insert => Avalonia.Input.Key.Insert,
            Key.Delete => Avalonia.Input.Key.Delete,
            Key.Right => Avalonia.Input.Key.Right,
            Key.Left => Avalonia.Input.Key.Left,
            Key.Down => Avalonia.Input.Key.Down,
            Key.Up => Avalonia.Input.Key.Up,
            Key.PageUp => Avalonia.Input.Key.PageUp,
            Key.PageDown => Avalonia.Input.Key.PageDown,
            Key.Home => Avalonia.Input.Key.Home,
            Key.End => Avalonia.Input.Key.End,
            Key.CapsLock => Avalonia.Input.Key.CapsLock,
            Key.ScrollLock => Avalonia.Input.Key.Scroll,
            Key.NumLock => Avalonia.Input.Key.NumLock,
            Key.PrintScreen => Avalonia.Input.Key.PrintScreen,
            Key.Pause => Avalonia.Input.Key.Pause,
            Key.F1 => Avalonia.Input.Key.F1,
            Key.F2 => Avalonia.Input.Key.F2,
            Key.F3 => Avalonia.Input.Key.F3,
            Key.F4 => Avalonia.Input.Key.F4,
            Key.F5 => Avalonia.Input.Key.F5,
            Key.F6 => Avalonia.Input.Key.F6,
            Key.F7 => Avalonia.Input.Key.F7,
            Key.F8 => Avalonia.Input.Key.F8,
            Key.F9 => Avalonia.Input.Key.F9,
            Key.F10 => Avalonia.Input.Key.F10,
            Key.F11 => Avalonia.Input.Key.F11,
            Key.F12 => Avalonia.Input.Key.F12,
            Key.F13 => Avalonia.Input.Key.F13,
            Key.F14 => Avalonia.Input.Key.F14,
            Key.F15 => Avalonia.Input.Key.F15,
            Key.F16 => Avalonia.Input.Key.F16,
            Key.F17 => Avalonia.Input.Key.F17,
            Key.F18 => Avalonia.Input.Key.F18,
            Key.F19 => Avalonia.Input.Key.F19,
            Key.F20 => Avalonia.Input.Key.F20,
            Key.F21 => Avalonia.Input.Key.F21,
            Key.F22 => Avalonia.Input.Key.F22,
            Key.F23 => Avalonia.Input.Key.F23,
            Key.F24 => Avalonia.Input.Key.F24,
            Key.Keypad0 => Avalonia.Input.Key.NumPad0,
            Key.Keypad1 => Avalonia.Input.Key.NumPad1,
            Key.Keypad2 => Avalonia.Input.Key.NumPad2,
            Key.Keypad3 => Avalonia.Input.Key.NumPad3,
            Key.Keypad4 => Avalonia.Input.Key.NumPad4,
            Key.Keypad5 => Avalonia.Input.Key.NumPad5,
            Key.Keypad6 => Avalonia.Input.Key.NumPad6,
            Key.Keypad7 => Avalonia.Input.Key.NumPad7,
            Key.Keypad8 => Avalonia.Input.Key.NumPad8,
            Key.Keypad9 => Avalonia.Input.Key.NumPad9,
            Key.KeypadDecimal => Avalonia.Input.Key.Decimal,
            Key.KeypadDivide => Avalonia.Input.Key.Divide,
            Key.KeypadMultiply => Avalonia.Input.Key.Multiply,
            Key.KeypadSubtract => Avalonia.Input.Key.Subtract,
            Key.KeypadAdd => Avalonia.Input.Key.Add,
            Key.KeypadEnter => Avalonia.Input.Key.Enter,
            Key.ShiftLeft => Avalonia.Input.Key.LeftShift,
            Key.ControlLeft => Avalonia.Input.Key.LeftCtrl,
            Key.AltLeft => Avalonia.Input.Key.LeftAlt,
            Key.SuperLeft => Avalonia.Input.Key.LWin,
            Key.ShiftRight => Avalonia.Input.Key.RightShift,
            Key.ControlRight => Avalonia.Input.Key.RightCtrl,
            Key.AltRight => Avalonia.Input.Key.RightAlt,
            Key.SuperRight => Avalonia.Input.Key.RWin,
            _ => Avalonia.Input.Key.None
        };

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext((object, RoutedEventArgs) value)
    {
        
    }
}