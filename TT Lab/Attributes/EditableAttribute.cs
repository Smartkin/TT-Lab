using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class EditableAttribute : Attribute
{
    public string Caption { get; init; } = string.Empty;
    public string? Hint { get; init; }
    public Type? EditorDescType { get; set; }
    public bool IsConstructible { get; init; }
    public int MaxLinkGraphDepth { get; init; } = int.MaxValue;

    // The getter makes the value anew from other data every time (the scenery's bounds): editing a part of it sets the whole value, set
    // on its own the part changed a copy nothing kept
    public bool IsComputed { get; init; }

    // The value has no nodes of its parts, its editor replaces it whole (a shader's animation): copying values copies it as the JSON its
    // asset keeps it as and pasting puts that in whole. Without nodes it wasn't copied at all
    public bool IsCopiedWhole { get; init; }
    
    public Avalonia.Controls.Dock EditorOrientation { get; init; } = Avalonia.Controls.Dock.Left;
}