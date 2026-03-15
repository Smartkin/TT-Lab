using System;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Descs;

public abstract partial record EditorDesc
{
    public required DocumentViewModel Document { get; init; }
    public required PropertyNode Node { get; init; }

    public DocumentNodeViewModel Construct()
    {
        var result = ConstructInternal();
        result.Orientation = Node.Metadata?.Editable?.EditorOrientation ?? Avalonia.Controls.Dock.Left;
        var caption = Node.Metadata?.Editable?.Caption;
        if (string.IsNullOrEmpty(caption))
        {
            caption = ToSpacedWords(Node.Name);
        }
        result.Caption = caption;
        result.Hint = Node.Metadata?.Editable?.Hint;
        result.IsEditable = !Node.IsReadOnly;
        result.EditorParameters = Node.Metadata?.EditorParams ?? new Dictionary<String, Object>();
        ApplyAttributeWrappers(result);
        return result;
    }
    
    protected virtual DocumentNodeViewModel ConstructInternal() => new DocumentModelViewModel(Document, Node);
    
    private void ApplyAttributeWrappers(DocumentNodeViewModel documentNode)
    {
        var wrappers = Node.Metadata?.EditorParamWrappers;
        if (wrappers == null)
        {
            return;
        }
        
        foreach (var wrapper in wrappers)
        {
            wrapper.ApplyTo(documentNode);
        }
    }
    
    private static string ToSpacedWords(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var spaced = SpaceSplitRegex().Replace(input, " ");
        spaced = DuplicatedSpacesRegex().Replace(spaced, " ").Trim();

        return spaced.Length > 0 ? $"{char.ToUpperInvariant(spaced[0])}{spaced[1..]}" : spaced;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex DuplicatedSpacesRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!^)(?=[A-Z][a-z])|(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial System.Text.RegularExpressions.Regex SpaceSplitRegex();
}