using System;
using System.Collections.Generic;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public abstract record EditorMetadata
{
    public Type? EditorDescType { get; init; }
    public EditableAttribute? Editable { get; init; }
    public EditorParamWrapperBaseAttribute[] EditorParamWrappers { get; init; }
    public Dictionary<string, List<IFieldChange>> FieldReactors { get; init; }
    public Dictionary<string, object> EditorParams { get; init; }
}