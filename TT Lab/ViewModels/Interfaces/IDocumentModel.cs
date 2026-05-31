using System;
using System.Collections.Generic;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Interfaces;

public interface IDocumentModel : IDisposable
{
    string DocumentName { get; }

    void Save() { }

    void IDisposable.Dispose() { }

    List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property) => [];
}


public record ViewportObject(EditableObject Render, string DocumentName, PropertyNode Property, object? UserData = null)
{
    public PropertyNode? Position { get; init; }
    public PropertyNode? Rotation { get; init; }
    public PropertyNode? Scale { get; init; }
    public PropertyNode? Transform { get; init; }

    public bool IsTransformSupported(TransformMode mode)
    {
        if (Transform != null)
        {
            return true;
        }

        if (mode == TransformMode.TRANSLATE)
        {
            return Position != null;
        }

        if (mode == TransformMode.ROTATE)
        {
            return Rotation != null;
        }

        if (mode == TransformMode.SCALE)
        {
            return Scale != null;
        }
        
        return false;
    }
}
