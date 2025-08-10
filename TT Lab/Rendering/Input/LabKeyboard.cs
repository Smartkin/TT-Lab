using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Silk.NET.Input;
using Key = Silk.NET.Input.Key;

namespace TT_Lab.Rendering.Input;

public class LabKeyboard : IKeyboard, IDisposable
{
    private static readonly Key[] Keys = Enum.GetValues<System.Windows.Input.Key>().Select(ConvertKey)
        .Where(static x => x != Key.Unknown).Distinct().ToArray();

    private readonly Image _renderArea;
    public string Name => "TT Lab Keyboard";
    public int Index => 0;
    public bool IsConnected => true;

    public LabKeyboard(Image renderArea)
    {
        Keyboard.AddKeyDownHandler(renderArea, KeyDownHandler);
        Keyboard.AddKeyUpHandler(renderArea, KeyUpHandler);
        _renderArea = renderArea;
    }

    public void Dispose()
    {
        Keyboard.RemoveKeyDownHandler(_renderArea, KeyDownHandler);
        Keyboard.RemoveKeyUpHandler(_renderArea, KeyUpHandler);
        GC.SuppressFinalize(this);
    }

    private void KeyUpHandler(object sender, KeyEventArgs e)
    {
        var scanCode =
            (int)typeof(KeyEventArgs).GetProperty("ScanCode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(e)!;
        KeyUp?.Invoke(this, ConvertKey(e.Key), scanCode);
        e.Handled = true;
    }

    private void KeyDownHandler(object sender, KeyEventArgs e)
    {
        var scanCode =
            (int)typeof(KeyEventArgs).GetProperty("ScanCode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(e)!;
        KeyDown?.Invoke(this, ConvertKey(e.Key), scanCode);

        var character = KeyToChar(e.Key);
        if (character != '\0')
        {
            KeyChar?.Invoke(this, character);
        }

        e.Handled = true;
    }

    public Boolean IsKeyPressed(Key key)
    {
        return Keyboard.IsKeyDown(ConvertKey(key));
    }

    public Boolean IsScancodePressed(int scancode) => false;

    public void BeginInput()
    {
    }

    public void EndInput()
    {
    }

    public IReadOnlyList<Key> SupportedKeys => Keys;

    public string ClipboardText
    {
        get => Clipboard.GetText();
        set => Clipboard.SetText(value);
    }

    public event Action<IKeyboard, Key, Int32>? KeyDown;
    public event Action<IKeyboard, Key, Int32>? KeyUp;
    public event Action<IKeyboard, Char>? KeyChar;

    private static char KeyToChar(System.Windows.Input.Key key)
    {
        if (Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) ||
            Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt) ||
            Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) ||
            Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt))
        {
            return '\0';
        }

        var caplock = Console.CapsLock;
        var shift = Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                     Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
        var iscap = (caplock && !shift) || (!caplock && shift);
        return key switch
        {
            System.Windows.Input.Key.Enter => '\n',
            System.Windows.Input.Key.A => (iscap ? 'A' : 'a'),
            System.Windows.Input.Key.B => (iscap ? 'B' : 'b'),
            System.Windows.Input.Key.C => (iscap ? 'C' : 'c'),
            System.Windows.Input.Key.D => (iscap ? 'D' : 'd'),
            System.Windows.Input.Key.E => (iscap ? 'E' : 'e'),
            System.Windows.Input.Key.F => (iscap ? 'F' : 'f'),
            System.Windows.Input.Key.G => (iscap ? 'G' : 'g'),
            System.Windows.Input.Key.H => (iscap ? 'H' : 'h'),
            System.Windows.Input.Key.I => (iscap ? 'I' : 'i'),
            System.Windows.Input.Key.J => (iscap ? 'J' : 'j'),
            System.Windows.Input.Key.K => (iscap ? 'K' : 'k'),
            System.Windows.Input.Key.L => (iscap ? 'L' : 'l'),
            System.Windows.Input.Key.M => (iscap ? 'M' : 'm'),
            System.Windows.Input.Key.N => (iscap ? 'N' : 'n'),
            System.Windows.Input.Key.O => (iscap ? 'O' : 'o'),
            System.Windows.Input.Key.P => (iscap ? 'P' : 'p'),
            System.Windows.Input.Key.Q => (iscap ? 'Q' : 'q'),
            System.Windows.Input.Key.R => (iscap ? 'R' : 'r'),
            System.Windows.Input.Key.S => (iscap ? 'S' : 's'),
            System.Windows.Input.Key.T => (iscap ? 'T' : 't'),
            System.Windows.Input.Key.U => (iscap ? 'U' : 'u'),
            System.Windows.Input.Key.V => (iscap ? 'V' : 'v'),
            System.Windows.Input.Key.W => (iscap ? 'W' : 'w'),
            System.Windows.Input.Key.X => (iscap ? 'X' : 'x'),
            System.Windows.Input.Key.Y => (iscap ? 'Y' : 'y'),
            System.Windows.Input.Key.Z => (iscap ? 'Z' : 'z'),
            System.Windows.Input.Key.D0 => (shift ? ')' : '0'),
            System.Windows.Input.Key.D1 => (shift ? '!' : '1'),
            System.Windows.Input.Key.D2 => (shift ? '@' : '2'),
            System.Windows.Input.Key.D3 => (shift ? '#' : '3'),
            System.Windows.Input.Key.D4 => (shift ? '$' : '4'),
            System.Windows.Input.Key.D5 => (shift ? '%' : '5'),
            System.Windows.Input.Key.D6 => (shift ? '^' : '6'),
            System.Windows.Input.Key.D7 => (shift ? '&' : '7'),
            System.Windows.Input.Key.D8 => (shift ? '*' : '8'),
            System.Windows.Input.Key.D9 => (shift ? '(' : '9'),
            System.Windows.Input.Key.OemPlus => (shift ? '+' : '='),
            System.Windows.Input.Key.OemMinus => (shift ? '_' : '-'),
            System.Windows.Input.Key.OemQuestion => (shift ? '?' : '/'),
            System.Windows.Input.Key.OemComma => (shift ? '<' : ','),
            System.Windows.Input.Key.OemPeriod => (shift ? '>' : '.'),
            System.Windows.Input.Key.OemOpenBrackets => (shift ? '{' : '['),
            System.Windows.Input.Key.OemQuotes => (shift ? '"' : '\''),
            System.Windows.Input.Key.Oem1 => (shift ? ':' : ';'),
            System.Windows.Input.Key.Oem3 => (shift ? '~' : '`'),
            System.Windows.Input.Key.Oem5 => (shift ? '|' : '\\'),
            System.Windows.Input.Key.Oem6 => (shift ? '}' : ']'),
            System.Windows.Input.Key.Tab => '\t',
            System.Windows.Input.Key.Space => ' ',
            // Number Pad
            System.Windows.Input.Key.NumPad0 => '0',
            System.Windows.Input.Key.NumPad1 => '1',
            System.Windows.Input.Key.NumPad2 => '2',
            System.Windows.Input.Key.NumPad3 => '3',
            System.Windows.Input.Key.NumPad4 => '4',
            System.Windows.Input.Key.NumPad5 => '5',
            System.Windows.Input.Key.NumPad6 => '6',
            System.Windows.Input.Key.NumPad7 => '7',
            System.Windows.Input.Key.NumPad8 => '8',
            System.Windows.Input.Key.NumPad9 => '9',
            System.Windows.Input.Key.Subtract => '-',
            System.Windows.Input.Key.Add => '+',
            System.Windows.Input.Key.Decimal => '.',
            System.Windows.Input.Key.Divide => '/',
            System.Windows.Input.Key.Multiply => '*',
            _ => '\0'
        };
    }

    private static Key ConvertKey(System.Windows.Input.Key keys) =>
        keys switch
        {
            System.Windows.Input.Key.None => Key.Unknown,
            System.Windows.Input.Key.Space => Key.Space,
            System.Windows.Input.Key.OemComma => Key.Comma,
            System.Windows.Input.Key.OemMinus => Key.Minus,
            System.Windows.Input.Key.OemPeriod => Key.Period,
            System.Windows.Input.Key.D0 => Key.Number0,
            System.Windows.Input.Key.D1 => Key.Number1,
            System.Windows.Input.Key.D2 => Key.Number2,
            System.Windows.Input.Key.D3 => Key.Number3,
            System.Windows.Input.Key.D4 => Key.Number4,
            System.Windows.Input.Key.D5 => Key.Number5,
            System.Windows.Input.Key.D6 => Key.Number6,
            System.Windows.Input.Key.D7 => Key.Number7,
            System.Windows.Input.Key.D8 => Key.Number8,
            System.Windows.Input.Key.D9 => Key.Number9,
            System.Windows.Input.Key.OemSemicolon => Key.Semicolon,
            System.Windows.Input.Key.A => Key.A,
            System.Windows.Input.Key.B => Key.B,
            System.Windows.Input.Key.C => Key.C,
            System.Windows.Input.Key.D => Key.D,
            System.Windows.Input.Key.E => Key.E,
            System.Windows.Input.Key.F => Key.F,
            System.Windows.Input.Key.G => Key.G,
            System.Windows.Input.Key.H => Key.H,
            System.Windows.Input.Key.I => Key.I,
            System.Windows.Input.Key.J => Key.J,
            System.Windows.Input.Key.K => Key.K,
            System.Windows.Input.Key.L => Key.L,
            System.Windows.Input.Key.M => Key.M,
            System.Windows.Input.Key.N => Key.N,
            System.Windows.Input.Key.O => Key.O,
            System.Windows.Input.Key.P => Key.P,
            System.Windows.Input.Key.Q => Key.Q,
            System.Windows.Input.Key.R => Key.R,
            System.Windows.Input.Key.S => Key.S,
            System.Windows.Input.Key.T => Key.T,
            System.Windows.Input.Key.U => Key.U,
            System.Windows.Input.Key.V => Key.V,
            System.Windows.Input.Key.W => Key.W,
            System.Windows.Input.Key.X => Key.X,
            System.Windows.Input.Key.Y => Key.Y,
            System.Windows.Input.Key.Z => Key.Z,
            System.Windows.Input.Key.OemOpenBrackets => Key.LeftBracket,
            System.Windows.Input.Key.OemBackslash => Key.BackSlash,
            System.Windows.Input.Key.OemCloseBrackets => Key.RightBracket,
            System.Windows.Input.Key.Escape => Key.Escape,
            System.Windows.Input.Key.Enter => Key.Enter,
            System.Windows.Input.Key.Tab => Key.Tab,
            System.Windows.Input.Key.Back => Key.Backspace,
            System.Windows.Input.Key.Insert => Key.Insert,
            System.Windows.Input.Key.Delete => Key.Delete,
            System.Windows.Input.Key.Right => Key.Right,
            System.Windows.Input.Key.Left => Key.Left,
            System.Windows.Input.Key.Down => Key.Down,
            System.Windows.Input.Key.Up => Key.Up,
            System.Windows.Input.Key.PageUp => Key.PageUp,
            System.Windows.Input.Key.PageDown => Key.PageDown,
            System.Windows.Input.Key.Home => Key.Home,
            System.Windows.Input.Key.End => Key.End,
            System.Windows.Input.Key.CapsLock => Key.CapsLock,
            System.Windows.Input.Key.Scroll => Key.ScrollLock,
            System.Windows.Input.Key.NumLock => Key.NumLock,
            System.Windows.Input.Key.PrintScreen => Key.PrintScreen,
            System.Windows.Input.Key.Pause => Key.Pause,
            System.Windows.Input.Key.F1 => Key.F1,
            System.Windows.Input.Key.F2 => Key.F2,
            System.Windows.Input.Key.F3 => Key.F3,
            System.Windows.Input.Key.F4 => Key.F4,
            System.Windows.Input.Key.F5 => Key.F5,
            System.Windows.Input.Key.F6 => Key.F6,
            System.Windows.Input.Key.F7 => Key.F7,
            System.Windows.Input.Key.F8 => Key.F8,
            System.Windows.Input.Key.F9 => Key.F9,
            System.Windows.Input.Key.F10 => Key.F10,
            System.Windows.Input.Key.F11 => Key.F11,
            System.Windows.Input.Key.F12 => Key.F12,
            System.Windows.Input.Key.F13 => Key.F13,
            System.Windows.Input.Key.F14 => Key.F14,
            System.Windows.Input.Key.F15 => Key.F15,
            System.Windows.Input.Key.F16 => Key.F16,
            System.Windows.Input.Key.F17 => Key.F17,
            System.Windows.Input.Key.F18 => Key.F18,
            System.Windows.Input.Key.F19 => Key.F19,
            System.Windows.Input.Key.F20 => Key.F20,
            System.Windows.Input.Key.F21 => Key.F21,
            System.Windows.Input.Key.F22 => Key.F22,
            System.Windows.Input.Key.F23 => Key.F23,
            System.Windows.Input.Key.F24 => Key.F24,
            System.Windows.Input.Key.NumPad0 => Key.Keypad0,
            System.Windows.Input.Key.NumPad1 => Key.Keypad1,
            System.Windows.Input.Key.NumPad2 => Key.Keypad2,
            System.Windows.Input.Key.NumPad3 => Key.Keypad3,
            System.Windows.Input.Key.NumPad4 => Key.Keypad4,
            System.Windows.Input.Key.NumPad5 => Key.Keypad5,
            System.Windows.Input.Key.NumPad6 => Key.Keypad6,
            System.Windows.Input.Key.NumPad7 => Key.Keypad7,
            System.Windows.Input.Key.NumPad8 => Key.Keypad8,
            System.Windows.Input.Key.NumPad9 => Key.Keypad9,
            System.Windows.Input.Key.Decimal => Key.KeypadDecimal,
            System.Windows.Input.Key.Divide => Key.KeypadDivide,
            System.Windows.Input.Key.Multiply => Key.KeypadMultiply,
            System.Windows.Input.Key.Subtract => Key.KeypadSubtract,
            System.Windows.Input.Key.Add => Key.KeypadAdd,
            System.Windows.Input.Key.LeftShift => Key.ShiftLeft,
            System.Windows.Input.Key.LeftCtrl => Key.ControlLeft,
            System.Windows.Input.Key.LeftAlt => Key.AltLeft,
            System.Windows.Input.Key.LWin => Key.SuperLeft,
            System.Windows.Input.Key.RightShift => Key.ShiftRight,
            System.Windows.Input.Key.RightCtrl => Key.ControlRight,
            System.Windows.Input.Key.RightAlt => Key.AltRight,
            System.Windows.Input.Key.RWin => Key.SuperRight,
            _ => Key.Unknown
        };

    private static System.Windows.Input.Key ConvertKey(Key keys) =>
        keys switch
        {
            Key.Unknown => System.Windows.Input.Key.None,
            Key.Space => System.Windows.Input.Key.Space,
            Key.Comma => System.Windows.Input.Key.OemComma,
            Key.Minus => System.Windows.Input.Key.OemMinus,
            Key.Period => System.Windows.Input.Key.Decimal,
            Key.Slash => System.Windows.Input.Key.Divide,
            Key.Number0 => System.Windows.Input.Key.D0,
            Key.Number1 => System.Windows.Input.Key.D1,
            Key.Number2 => System.Windows.Input.Key.D2,
            Key.Number3 => System.Windows.Input.Key.D3,
            Key.Number4 => System.Windows.Input.Key.D4,
            Key.Number5 => System.Windows.Input.Key.D5,
            Key.Number6 => System.Windows.Input.Key.D6,
            Key.Number7 => System.Windows.Input.Key.D7,
            Key.Number8 => System.Windows.Input.Key.D8,
            Key.Number9 => System.Windows.Input.Key.D9,
            Key.Semicolon => System.Windows.Input.Key.OemSemicolon,
            Key.A => System.Windows.Input.Key.A,
            Key.B => System.Windows.Input.Key.B,
            Key.C => System.Windows.Input.Key.C,
            Key.D => System.Windows.Input.Key.D,
            Key.E => System.Windows.Input.Key.E,
            Key.F => System.Windows.Input.Key.F,
            Key.G => System.Windows.Input.Key.G,
            Key.H => System.Windows.Input.Key.H,
            Key.I => System.Windows.Input.Key.I,
            Key.J => System.Windows.Input.Key.J,
            Key.K => System.Windows.Input.Key.K,
            Key.L => System.Windows.Input.Key.L,
            Key.M => System.Windows.Input.Key.M,
            Key.N => System.Windows.Input.Key.N,
            Key.O => System.Windows.Input.Key.O,
            Key.P => System.Windows.Input.Key.P,
            Key.Q => System.Windows.Input.Key.Q,
            Key.R => System.Windows.Input.Key.R,
            Key.S => System.Windows.Input.Key.S,
            Key.T => System.Windows.Input.Key.T,
            Key.U => System.Windows.Input.Key.U,
            Key.V => System.Windows.Input.Key.V,
            Key.W => System.Windows.Input.Key.W,
            Key.X => System.Windows.Input.Key.X,
            Key.Y => System.Windows.Input.Key.Y,
            Key.Z => System.Windows.Input.Key.Z,
            Key.Escape => System.Windows.Input.Key.Escape,
            Key.Enter => System.Windows.Input.Key.Enter,
            Key.Tab => System.Windows.Input.Key.Tab,
            Key.Backspace => System.Windows.Input.Key.Back,
            Key.Insert => System.Windows.Input.Key.Insert,
            Key.Delete => System.Windows.Input.Key.Delete,
            Key.Right => System.Windows.Input.Key.Right,
            Key.Left => System.Windows.Input.Key.Left,
            Key.Down => System.Windows.Input.Key.Down,
            Key.Up => System.Windows.Input.Key.Up,
            Key.PageUp => System.Windows.Input.Key.PageUp,
            Key.PageDown => System.Windows.Input.Key.PageDown,
            Key.Home => System.Windows.Input.Key.Home,
            Key.End => System.Windows.Input.Key.End,
            Key.CapsLock => System.Windows.Input.Key.CapsLock,
            Key.ScrollLock => System.Windows.Input.Key.Scroll,
            Key.NumLock => System.Windows.Input.Key.NumLock,
            Key.PrintScreen => System.Windows.Input.Key.PrintScreen,
            Key.Pause => System.Windows.Input.Key.Pause,
            Key.F1 => System.Windows.Input.Key.F1,
            Key.F2 => System.Windows.Input.Key.F2,
            Key.F3 => System.Windows.Input.Key.F3,
            Key.F4 => System.Windows.Input.Key.F4,
            Key.F5 => System.Windows.Input.Key.F5,
            Key.F6 => System.Windows.Input.Key.F6,
            Key.F7 => System.Windows.Input.Key.F7,
            Key.F8 => System.Windows.Input.Key.F8,
            Key.F9 => System.Windows.Input.Key.F9,
            Key.F10 => System.Windows.Input.Key.F10,
            Key.F11 => System.Windows.Input.Key.F11,
            Key.F12 => System.Windows.Input.Key.F12,
            Key.F13 => System.Windows.Input.Key.F13,
            Key.F14 => System.Windows.Input.Key.F14,
            Key.F15 => System.Windows.Input.Key.F15,
            Key.F16 => System.Windows.Input.Key.F16,
            Key.F17 => System.Windows.Input.Key.F17,
            Key.F18 => System.Windows.Input.Key.F18,
            Key.F19 => System.Windows.Input.Key.F19,
            Key.F20 => System.Windows.Input.Key.F20,
            Key.F21 => System.Windows.Input.Key.F21,
            Key.F22 => System.Windows.Input.Key.F22,
            Key.F23 => System.Windows.Input.Key.F23,
            Key.F24 => System.Windows.Input.Key.F24,
            Key.Keypad0 => System.Windows.Input.Key.NumPad0,
            Key.Keypad1 => System.Windows.Input.Key.NumPad1,
            Key.Keypad2 => System.Windows.Input.Key.NumPad2,
            Key.Keypad3 => System.Windows.Input.Key.NumPad3,
            Key.Keypad4 => System.Windows.Input.Key.NumPad4,
            Key.Keypad5 => System.Windows.Input.Key.NumPad5,
            Key.Keypad6 => System.Windows.Input.Key.NumPad6,
            Key.Keypad7 => System.Windows.Input.Key.NumPad7,
            Key.Keypad8 => System.Windows.Input.Key.NumPad8,
            Key.Keypad9 => System.Windows.Input.Key.NumPad9,
            Key.KeypadDecimal => System.Windows.Input.Key.Decimal,
            Key.KeypadDivide => System.Windows.Input.Key.Divide,
            Key.KeypadMultiply => System.Windows.Input.Key.Multiply,
            Key.KeypadSubtract => System.Windows.Input.Key.Subtract,
            Key.KeypadAdd => System.Windows.Input.Key.Add,
            Key.KeypadEnter => System.Windows.Input.Key.Enter,
            Key.ShiftLeft => System.Windows.Input.Key.LeftShift,
            Key.ControlLeft => System.Windows.Input.Key.LeftCtrl,
            Key.AltLeft => System.Windows.Input.Key.LeftAlt,
            Key.SuperLeft => System.Windows.Input.Key.LWin,
            Key.ShiftRight => System.Windows.Input.Key.RightShift,
            Key.ControlRight => System.Windows.Input.Key.RightCtrl,
            Key.AltRight => System.Windows.Input.Key.RightAlt,
            Key.SuperRight => System.Windows.Input.Key.RWin,
            _ => System.Windows.Input.Key.None
        };
}