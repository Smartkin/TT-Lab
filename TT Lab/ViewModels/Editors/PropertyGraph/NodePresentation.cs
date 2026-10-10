namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// How a node's editor shows what its owner makes of the value right now, set by a linked field (a shader's parameters by its type):
/// hidden, or shown with a caption and a hint of its own where they're given (the editor's own otherwise)
/// </summary>
public sealed record NodePresentation(bool IsHidden, string? Caption = null, string? Hint = null)
{
    public static readonly NodePresentation Hidden = new(true);
}
