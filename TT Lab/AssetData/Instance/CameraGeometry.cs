using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// What a camera's subtypes derive from their geometry and the viewport handles that edit it
/// </summary>
public static class CameraGeometry
{
    private const float PointHandleAlpha = 0.5f;
    private const float SelectedHandleAlpha = 0.25f;

    /// <summary>
    /// The geometry the derived values and handles depend on: the points, the boxes or the matrices, with the kind of camera
    /// </summary>
    public sealed record Shape(Type? Kind, float[] Values, int Count);

    public static Shape Snapshot(CameraSubBase? camera)
    {
        return camera switch
        {
            null => new Shape(null, [], 0),
            CameraPath path => new Shape(typeof(CameraPath), Flatten(path.PathPoints), path.PathPoints.Count),
            CameraSpline spline => new Shape(typeof(CameraSpline), Flatten(spline.PathPoints), spline.PathPoints.Count),
            BossCamera boss => new Shape(typeof(BossCamera), Flatten(Columns(boss.WorldToArena).Concat(Columns(boss.ArenaToWorld))), 1),
            CameraZone zone => new Shape(typeof(CameraZone), Flatten(zone.CameraBox.Concat(zone.TargetBox)), 2),
            CameraLine line => new Shape(typeof(CameraLine), Flatten([line.LineStart, line.LineEnd]), 2),
            CameraLine2 line => new Shape(typeof(CameraLine2), Flatten([line.LineStart, line.LineEnd]), 2),
            CameraPoint point => new Shape(typeof(CameraPoint), Flatten([point.Point]), 1),
            CameraPoint2 point => new Shape(typeof(CameraPoint2), Flatten([point.Point]), 1),
            _ => new Shape(camera.GetType(), [], 0),
        };
    }

    public static bool SameSnapshot(Shape a, Shape b) => a.Kind == b.Kind && a.Values.AsSpan().SequenceEqual(b.Values);

    public static bool SameLayout(Shape a, Shape b) => a.Kind == b.Kind && a.Count == b.Count;

    /// <summary>
    /// Works out what the game stores next to the geometry: a path's parameters, a spline's tangents, step and parameters, the boss
    /// camera's other matrix from the one that changed
    /// </summary>
    public static void UpdateDerived(CameraSubBase camera, Shape? previous)
    {
        switch (camera)
        {
            case CameraPath path:
            {
                var points = path.PathPoints.Select(point => new vec3(point.X, point.Y, point.Z)).ToArray();
                var stepLength = PathParameters.FindStepLength(path.ArcLengths, path.InverseSteps, path.ArcLengths.Count) ?? PathParameters.DefaultStepLength;
                (path.ArcLengths, path.InverseSteps) = PathParameters.Create(points, stepLength);
                break;
            }
            case CameraSpline spline:
                UpdateSpline(spline);
                break;
            case BossCamera boss:
            {
                // The game uses each matrix as the other's inverse, the arena's own matrix is the one edited (World To Arena follows it
                // in the inspector too, ForeignPropertyAttributes)
                var arenaToWorld = boss.ArenaToWorld.ToGlm();
                var previousArenaToWorld = previous?.Values.Length >= 32 ? Matrix(previous.Values.AsSpan(16, 16)) : (mat4?)null;
                if ((previousArenaToWorld == null || !Same(previousArenaToWorld.Value, arenaToWorld)) && MathF.Abs(arenaToWorld.Determinant) > 1e-12f)
                {
                    boss.WorldToArena = arenaToWorld.Inverse.ToTwin();
                }

                break;
            }
        }
    }

    // The samples stay where they were put, the tangents point along the curve through them, the segments' lengths are the chords
    // (the game's lengths are, its samples lie a step apart) and every segment keeps taking the steps it took
    private static void UpdateSpline(CameraSpline spline)
    {
        var points = spline.PathPoints.Select(point => new vec3(point.X, point.Y, point.Z)).ToArray();
        var segments = Math.Max(points.Length - 1, 0);
        var tangents = new List<Vector4>(points.Length);
        for (var i = 0; i < points.Length; i++)
        {
            var from = points[Math.Max(i - 1, 0)];
            var to = points[Math.Min(i + 1, points.Length - 1)];
            var tangent = (to - from).LengthSqr > 1e-12f ? (to - from).Normalized : vec3.UnitZ;
            tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
        }

        var lengths = new float[segments];
        var total = 0.0f;
        for (var i = 0; i < segments; i++)
        {
            total += (points[i + 1] - points[i]).Length;
            lengths[i] = total;
        }

        var stepLength = PathParameters.FindStepLength(spline.ArcLengths, spline.InverseSteps, spline.ArcLengths.Count) ?? PathParameters.DefaultStepLength;
        var inverseSteps = new List<Single>(segments);
        for (var i = 0; i < segments; i++)
        {
            var length = lengths[i] - (i > 0 ? lengths[i - 1] : 0.0f);
            inverseSteps.Add(1.0f / PathParameters.GetSteps(length, stepLength));
        }

        spline.Tangents = tangents;
        spline.ArcLengths = [..lengths];
        spline.InverseSteps = inverseSteps;
        spline.StepLength = segments > 0 ? total / segments : spline.StepLength;
    }

    /// <summary>
    /// The objects the viewport edits a camera's geometry with: a handle for every point, a box for every zone box, the boss camera's
    /// arena. Each edits its own node, so the inspector follows.
    /// </summary>
    public static List<ViewportObject> CreateHandles(ViewportContext context, PropertyNode property, PropertyNode cameraProperty, CameraSubBase? camera, string name, vec4 color)
    {
        var objects = new List<ViewportObject>();
        switch (camera)
        {
            case CameraPoint:
            case CameraPoint2:
                AddPoint(objects, context, property, cameraProperty, "Point", $"{name}_POINT", color, null);
                break;
            case CameraLine:
            case CameraLine2:
                AddPoint(objects, context, property, cameraProperty, "LineStart", $"{name}_START", color, null);
                AddPoint(objects, context, property, cameraProperty, "LineEnd", $"{name}_END", color, null);
                break;
            case CameraPath path:
                for (var i = 0; i < path.PathPoints.Count; i++)
                {
                    AddPoint(objects, context, property, cameraProperty, $"PathPoints[{i}]", $"{name}_POINT_{i}", color, cameraProperty.Find($"PathPoints[{i}]"));
                }

                break;
            case CameraSpline spline:
                for (var i = 0; i < spline.PathPoints.Count; i++)
                {
                    AddPoint(objects, context, property, cameraProperty, $"PathPoints[{i}]", $"{name}_POINT_{i}", color, cameraProperty.Find($"PathPoints[{i}]"));
                }

                break;
            case CameraZone:
                AddBox(objects, context, property, cameraProperty, nameof(CameraZone.CameraBoxTransform), $"{name}_CAMERA_BOX", color);
                AddBox(objects, context, property, cameraProperty, nameof(CameraZone.TargetBoxTransform), $"{name}_TARGET_BOX", new vec4(0.4f, 0.9f, 0.4f, 1.0f));
                break;
            case BossCamera boss:
                AddArena(objects, context, property, cameraProperty, boss, $"{name}_ARENA", color);
                break;
        }

        return objects;
    }

    private static void AddPoint(List<ViewportObject> objects, ViewportContext context, PropertyNode property, PropertyNode cameraProperty, string path, string name, vec4 color, PropertyNode? element)
    {
        var pointProperty = cameraProperty.Find(path);
        if (pointProperty?.GetValue() is not Vector4 point)
        {
            return;
        }

        var visual = context.EditingContext.CreatePathBillboard();
        visual.Diffuse = color with { w = PointHandleAlpha };
        var size = vec3.Ones * 2.0f;
        var offset = -vec3.Ones;
        var handle = new EditableObject(context.RenderContext, visual, name, offset, size)
        {
            SelectedColor = color with { w = SelectedHandleAlpha },
            UnselectedColor = visual.Diffuse,
        };
        handle.SetPosition(new vec3(point.X, point.Y, point.Z));
        objects.Add(new ViewportObject(handle, $"CAMERA_HANDLE_{pointProperty.Path}", property)
        {
            Position = pointProperty,
            Category = ViewportObjectCategory.CameraPaths,
            InspectorFocus = pointProperty,
            DuplicatedElement = element,
        });
    }

    private static void AddBox(List<ViewportObject> objects, ViewportContext context, PropertyNode property, PropertyNode cameraProperty, string path, string name, vec4 color)
    {
        var boxProperty = cameraProperty.Find(path);
        if (boxProperty?.GetValue() is not Matrix4 box)
        {
            return;
        }

        var visual = BufferGeneration.GetVolumeBuffer(context.RenderContext).Model!;
        visual.Diffuse = color with { w = BufferGeneration.VolumeOpacity };
        var converter = new UnitBoxConverter();
        var editable = new EditableObject(context.RenderContext, visual, name, -vec3.Ones, vec3.Ones * 2.0f)
        {
            SelectedColor = color with { w = BufferGeneration.SelectedVolumeOpacity },
            UnselectedColor = visual.Diffuse,
        };
        editable.SetLocalTransform(converter.ToTransform(box));
        objects.Add(new ViewportObject(editable, $"CAMERA_BOX_{boxProperty.Path}", property)
        {
            Transform = boxProperty,
            TransformConverter = converter,
            Category = ViewportObjectCategory.CameraPaths,
            InspectorFocus = cameraProperty,
        });
    }

    private static void AddArena(List<ViewportObject> objects, ViewportContext context, PropertyNode property, PropertyNode cameraProperty, BossCamera boss, string name, vec4 color)
    {
        var matrixProperty = cameraProperty.Find(nameof(BossCamera.ArenaToWorld));
        var orbitProperty = cameraProperty.Find(nameof(BossCamera.Orbit));
        if (matrixProperty?.GetValue() is not Matrix4 arenaToWorld || orbitProperty == null)
        {
            return;
        }

        var editable = new EditableObject(context.RenderContext, null, name, new vec3(-boss.Orbit.Y, 0, -boss.Orbit.Y), new vec3(boss.Orbit.Y * 2, Math.Max(boss.Orbit.X, 0.01f), boss.Orbit.Y * 2));
        editable.Init();
        editable.SetLocalTransform(arenaToWorld.ToGlm());
        var arena = new CameraArenaVisual(context.RenderContext, editable, color, boss.Orbit.Y, boss.Orbit.X);
        objects.Add(new ViewportObject(editable, $"CAMERA_ARENA_{matrixProperty.Path}", property)
        {
            Transform = matrixProperty,
            Category = ViewportObjectCategory.CameraPaths,
            InspectorFocus = cameraProperty,
            RenderDependencies = [orbitProperty],
            Refresh = () =>
            {
                arena.SetShape(boss.Orbit.Y, boss.Orbit.X);
                editable.Offset = new vec3(-boss.Orbit.Y, 0, -boss.Orbit.Y);
                editable.Size = new vec3(boss.Orbit.Y * 2, Math.Max(boss.Orbit.X, 0.01f), boss.Orbit.Y * 2);
                return true;
            },
        });
    }

    /// <summary>
    /// A zone box is a matrix taking the unit cube from its corner, the viewport's volume cube goes from -1 to 1
    /// </summary>
    public sealed class UnitBoxConverter : ITransformConverter
    {
        private static readonly mat4 UnitToVolume = mat4.Translate(new vec3(0.5f)) * mat4.Scale(0.5f);
        private static readonly mat4 VolumeToUnit = UnitToVolume.Inverse;

        public mat4 ToTransform(Matrix4 data) => data.ToGlm() * UnitToVolume;

        public Matrix4 ToData(mat4 transform) => (transform * VolumeToUnit).ToTwin();
    }

    private static float[] Flatten(IEnumerable<Vector4> vectors) => vectors.SelectMany(vector => new[] { vector.X, vector.Y, vector.Z, vector.W }).ToArray();

    private static IEnumerable<Vector4> Columns(Matrix4 matrix) => [matrix.Column1, matrix.Column2, matrix.Column3, matrix.Column4];

    private static mat4 Matrix(ReadOnlySpan<float> values)
    {
        return new mat4(new vec4(values[0], values[1], values[2], values[3]), new vec4(values[4], values[5], values[6], values[7]),
            new vec4(values[8], values[9], values[10], values[11]), new vec4(values[12], values[13], values[14], values[15]));
    }

    private static bool Same(mat4 a, mat4 b)
    {
        for (var i = 0; i < 16; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }
}
