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
    public bool IsExcludedFromPropertyGraph { get; init; }
    
    public Avalonia.Controls.Dock EditorOrientation { get; init; } = Avalonia.Controls.Dock.Left;
}