using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Interfaces;

public interface IDocumentModel : IDisposable
{
    string DocumentName { get; }

    void Save() { }

    void IDisposable.Dispose() { }

    List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property) => [];
}

/// <summary>
/// What the viewport can show or hide separately
/// </summary>
public enum ViewportObjectCategory
{
    Other,
    Scenery,
    DynamicScenery,
    Collision,
    Skydome,
    LinkedScenery,
    Instances,
    Triggers,
    Cameras,
    CameraPaths,
    Positions,
    Paths,
    AiPositions,
    AiPaths,
    Particles,
    LoadWalls,
}

/// <summary>
/// Turns data kept in a matrix that isn't a transform, like the corners of a load wall, into its object's transform and back
/// </summary>
public interface ITransformConverter
{
    mat4 ToTransform(Matrix4 data);
    Matrix4 ToData(mat4 transform);
}

public record ViewportObject(EditableObject Render, string DocumentName, PropertyNode Property, object? UserData = null)
{
    public PropertyNode? Position { get; init; }
    public PropertyNode? Rotation { get; init; }
    public PropertyNode? Scale { get; init; }
    public PropertyNode? Transform { get; init; }

    /// <summary>
    /// Set when the Transform node's matrix is something else than the object's transform
    /// </summary>
    public ITransformConverter? TransformConverter { get; init; }

    public ViewportObjectCategory Category { get; init; }

    /// <summary>
    /// Part of what the object belongs to that the inspector brings into view when it gets selected, a point of a path for example
    /// </summary>
    public PropertyNode? InspectorFocus { get; init; }

    /// <summary>
    /// Element of a list the object stands for (an emitter of the chunk's particles, a point of a path, a link to another chunk),
    /// duplicating the object puts a copy of it right after it. Objects without one are duplicated as the whole resource
    /// </summary>
    public PropertyNode? DuplicatedElement { get; init; }

    /// <summary>
    /// Anything the visuals depend on besides the transform
    /// </summary>
    public IReadOnlyList<PropertyNode> RenderDependencies { get; init; } = [];

    /// <summary>
    /// Updates the visuals after any of the render dependencies changed. Gets called on the UI thread, returns false when the objects of
    /// the resource have to be created from scratch instead
    /// </summary>
    public Func<bool>? Refresh { get; init; }

    // What the inspector doesn't let be edited can't be dragged either, like the load wall of a link that doesn't use it
    public bool IsTransformSupported(TransformMode mode)
    {
        if (Transform != null)
        {
            return !Transform.IsReadOnly;
        }

        var node = mode switch
        {
            TransformMode.TRANSLATE => Position,
            TransformMode.ROTATE => Rotation,
            TransformMode.SCALE => Scale,
            _ => null,
        };
        return node is { IsReadOnly: false };
    }

    public mat4 GetTransformFromData(Matrix4 data)
    {
        return TransformConverter?.ToTransform(data) ?? data.ToGlm();
    }

    public Matrix4 GetDataFromTransform(mat4 transform)
    {
        return TransformConverter?.ToData(transform) ?? transform.ToTwin();
    }
}
