using System;
using System.IO;
using System.Windows.Input;
using Silk.NET.Core;
using Silk.NET.Input;
using CursorType = Silk.NET.Input.CursorType;

namespace TT_Lab.Rendering.Input;

public class LabCursor : ICursor, IDisposable
{
    public Boolean IsSupported(CursorMode mode)
    {
        return mode switch
        {
            CursorMode.Normal or CursorMode.Hidden => true,
            _ => false
        };
    }

    public Boolean IsSupported(StandardCursor standardCursor)
    {
        return standardCursor switch
        {
            StandardCursor.Default or StandardCursor.Arrow or StandardCursor.IBeam or StandardCursor.Crosshair
                or StandardCursor.Hand or StandardCursor.HResize or StandardCursor.VResize or StandardCursor.ResizeAll
                or StandardCursor.NotAllowed or StandardCursor.Wait or StandardCursor.WaitArrow => true,
            _ => false
        };
    }

    private StandardCursor _standardCursor = StandardCursor.Default;
    private RawImage _image;
    private Cursor? _customCursorImage;
    private CursorMode _cursorMode;

    public CursorType Type { get; set; } = CursorType.Standard;

    public StandardCursor StandardCursor
    {
        get => _standardCursor;
        
        set
        {
            _standardCursor = value;
            Mouse.OverrideCursor = MapTypeToCursor(_standardCursor);
        }
    }

    public CursorMode CursorMode
    {
        get => _cursorMode;
        set
        {
            _cursorMode = value;
            Mouse.OverrideCursor = _cursorMode == CursorMode.Hidden ? Cursors.None : null;
        }
    }

    public bool IsConfined
    {
        get => false;
        set { }
    }
    public int HotspotX { get; set; }
    public int HotspotY { get; set; }
    public RawImage Image
    {
        get => _image;
        
        set
        {
            _customCursorImage?.Dispose();
            _image = value;
            _customCursorImage = new Cursor(new MemoryStream(_image.Pixels.Span.ToArray()), true);
            Mouse.OverrideCursor = _customCursorImage;
        }
    }

    private static Cursor? MapTypeToCursor(StandardCursor type)
    {
        return type switch
        {
            StandardCursor.Arrow => Cursors.Arrow,
            StandardCursor.IBeam => Cursors.IBeam,
            StandardCursor.Crosshair => Cursors.Cross,
            StandardCursor.Hand => Cursors.Hand,
            StandardCursor.HResize => Cursors.SizeWE,
            StandardCursor.VResize => Cursors.SizeNS,
            StandardCursor.NwseResize => Cursors.SizeNWSE,
            StandardCursor.NeswResize => Cursors.SizeNESW,
            StandardCursor.ResizeAll => Cursors.SizeAll,
            StandardCursor.NotAllowed => Cursors.No,
            StandardCursor.Wait => Cursors.Wait,
            _ => null
        };
    }

    public void Dispose()
    {
        _customCursorImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}