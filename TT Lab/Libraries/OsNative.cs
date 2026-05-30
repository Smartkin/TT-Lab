using System;
using System.Runtime.InteropServices;
using System.Windows;
using Avalonia;
using Avalonia.Controls;

namespace TT_Lab.Libraries
{
    public static partial class OsNative
    {
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetCursorPos(int x, int y);
        
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        };
        
        [LibraryImport("user32.dll")]
        private static partial void ClipCursor(ref Win32Rect lpRect);

        [LibraryImport("user32.dll")]
        private static partial void ClipCursor(IntPtr lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Rect
        {
            public int Left, Top, Right, Bottom;
        }

        private static Boolean _cursorClipped = false;

        public static void RestrictCursorToWindow(Window window)
        {
            if (_cursorClipped)
            {
                return;
            }
            
            var windowBounds = new Win32Rect
            {
                Left = window.Position.X,
                Top = window.Position.Y,
                Right = (int)(window.Position.X + window.Width),
                Bottom = (int)(window.Position.Y + window.Height)
            };

            ClipCursor(ref windowBounds);
            _cursorClipped = true;
        }

        public static void FreeCursor()
        {
            if (!_cursorClipped)
            {
                return;
            }
            
            // Have to pass null to free cursor
            ClipCursor(IntPtr.Zero);

            _cursorClipped = false;
        }
        
        public static Point GetMousePosition()
        {
            var w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);

            return new Point(w32Mouse.X, w32Mouse.Y);
        }
    }
}
