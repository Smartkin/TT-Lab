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
    DynamicSceneryBounds,
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
    LinkHulls,
    Lights,
    SceneryBounds,
}

/// <summary>
/// Turns data kept in a matrix that isn't a transform, like the corners of a load wall, into its object's transform and back
/// </summary>
public interface ITransformConverter
{
    mat4 ToTransform(Matrix4 data);
    Matrix4 ToData(mat4 transform);
}

/// <summary>
/// Turns a value kept in a position node that isn't the object's position, like the radius a handle's distance stands for, into where
/// the object is and back
/// </summary>
public interface IPositionConverter
{
    vec3 ToPosition(object? data);
    object ToData(vec3 position);
}

/// <summary>
/// Turns a value kept in a rotation node that isn't Euler angles, like the direction a light shines along, into the object's rotation and
/// back
/// </summary>
public interface IRotationConverter
{
    quat ToRotation(object? data);
    object ToData(quat rotation);
}

public record ViewportObject(EditableObject Render, string DocumentName, PropertyNode Property, object? UserData = null)
{
    public PropertyNode? Position { get; init; }

    /// <summary>
    /// Set when the Position node holds something else than the object's position. Such objects are handles of something the object
    /// they belong to has, they're picked on their own and never moved to the cursor with it
    /// </summary>
    public IPositionConverter? PositionConverter { get; init; }
    public PropertyNode? Rotation { get; init; }

    /// <summary>
    /// Set when the Rotation node holds something else than Euler angles in degrees
    /// </summary>
    public IRotationConverter? RotationConverter { get; init; }
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

    /// <summary>
    /// The value a position node takes for a point: its own kind of vector, a Vector4 keeping its W (a camera's points)
    /// </summary>
    public static object PositionValue(PropertyNode node, vec3 position)
    {
        if (node.GetValue() is Twinsanity.TwinsanityInterchange.Common.Vector4 previous)
        {
            return new Twinsanity.TwinsanityInterchange.Common.Vector4(position.x, position.y, position.z, previous.W);
        }

        return new Twinsanity.TwinsanityInterchange.Common.Vector3(position.x, position.y, position.z);
    }

    /// <summary>
    /// The value the position node takes for the object standing at a point
    /// </summary>
    public object PositionData(vec3 position)
    {
        return PositionConverter?.ToData(position) ?? PositionValue(Position!, position);
    }

    /// <summary>
    /// The value the rotation node takes for the object turned so
    /// </summary>
    public object RotationData(quat rotation)
    {
        if (RotationConverter != null)
        {
            return RotationConverter.ToData(rotation);
        }

        var degrees = vec3.Degrees(rotation.ToEulerAngles());
        return new Twinsanity.TwinsanityInterchange.Common.Vector3(degrees.x, degrees.y, degrees.z);
    }

    public Matrix4 GetDataFromTransform(mat4 transform)
    {
        return TransformConverter?.ToData(transform) ?? transform.ToTwin();
    }
}
