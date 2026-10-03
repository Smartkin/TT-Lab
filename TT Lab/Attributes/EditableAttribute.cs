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
    
    public Avalonia.Controls.Dock EditorOrientation { get; init; } = Avalonia.Controls.Dock.Left;
}