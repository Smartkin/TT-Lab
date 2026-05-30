using System;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public record PropertyMetadata : EditorMetadata
{
    public required PropertyInfo PropertyInfo { get; init; }
    public Func<object>? ContainedTypeConstructor { get; init; }
    public Dictionary<Type, Func<object?>> TypeConstructors { get; init; } = [];
}