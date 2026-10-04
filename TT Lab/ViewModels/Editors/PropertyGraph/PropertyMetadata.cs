using System;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public record PropertyMetadata : EditorMetadata
{
    private static readonly Dictionary<string, List<IFieldChange>> NoReactors = [];

    public required PropertyInfo PropertyInfo { get; init; }
    public Func<object>? ContainedTypeConstructor { get; init; }
    public Dictionary<Type, Func<object?>> TypeConstructors { get; init; } = [];

    // What a list's elements and a flags value's bits are made with: the property's editor attributes, but not the fields it's linked to,
    // which an element or a bit looked for among its own siblings (a warning for every instance a trigger listed, every key of a particle
    // curve, every bit of every trigger's activators)
    public PropertyMetadata ForParts() => this with { ContainedTypeConstructor = null, FieldReactors = NoReactors };
}