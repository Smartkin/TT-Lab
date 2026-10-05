using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Extensions;
using TT_Lab.Project.Prefabs;
using TT_Lab.Rendering.Scene;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// A part of a prefab as dragging it over a scene shows it, where it goes from the drop point: an object instance's model and turn
/// (<see cref="LabURI.Empty"/> for the box an object without a model is drawn as), none for a marker
/// </summary>
internal sealed record PrefabPreviewPart(LabURI? Model, vec3 Offset, quat Rotation);

/// <summary>
/// A prefab dragged over a chunk's scene, drawn where letting it go places it: its object instances' models and its scenery meshes tinted,
/// seen through walls as a silhouette (PreviewPass), with their boxes outlined, a box for the rest (positions, triggers, cameras, links,
/// emitters) and a ring on the drop point. Shown, moved and taken away on the render thread
/// </summary>
internal sealed class PrefabPreview(Prefab prefab, IReadOnlyList<PrefabPreviewPart> parts)
{
    // The silhouette of what's hidden behind the scene is this color, see-through
    private static readonly vec4 Tint = new(0.8f, 0.6f, 1.0f, 0.45f);
    // No other editor visual is magenta: models' hulls, paths and bounds are cyan, the selection orange
    private static readonly vec4 OutlineColor = new(1.0f, 0.45f, 0.85f, 1.0f);
    private const float MarkerHalfSize = 0.5f;
    private const float RingPixels = 14.0f;

    private Node? _root;
    private readonly List<EditableObject> _shown = [];
    private readonly List<vec3> _markers = [];
    private vec3 _at;

    public Prefab Prefab => prefab;

    /// <summary>
    /// The parts of an instance, group or element prefab. A scenery prefab's meshes are read when it's shown (<see cref="ReadScenery"/>)
    /// </summary>
    public static List<PrefabPreviewPart> PartsOf(Prefab prefab)
    {
        return prefab.Kind switch
        {
            PrefabKind.Instance => [PartOf(prefab.AssetType, prefab.Data, vec3.Zero)],
            PrefabKind.Group => prefab.Items?.Select(item => PartOf(item.AssetType, item.Data, new vec3(item.Offset[0], item.Offset[1], item.Offset[2]))).ToList() ?? [],
            PrefabKind.Scenery => [],
            _ => [new PrefabPreviewPart(null, vec3.Zero, quat.Identity)],
        };
    }

    // An object instance is drawn with its object's model the way the scene draws it, turned the same, anything else is a marker
    private static PrefabPreviewPart PartOf(string assetType, JObject data, vec3 offset)
    {
        var marker = new PrefabPreviewPart(null, offset, quat.Identity);
        if (assetType != typeof(TT_Lab.Assets.Instance.ObjectInstance).FullName || data[nameof(ObjectInstanceData.ObjectId)]?.ToObject<LabURI>() is not { } objectUri)
        {
            return marker;
        }

        var assetManager = AssetManager.Get();
        if (!assetManager.DoesAssetExist(objectUri) || assetManager.GetAsset(objectUri) is not GameObject gameObject)
        {
            return marker;
        }

        var model = ObjectInstanceData.ModelOf(((IAsset)gameObject).GetData<GameObjectData>());
        if (model != LabURI.Empty && !assetManager.DoesAssetExist(model))
        {
            return marker;
        }

        var rotation = data[nameof(ObjectInstanceData.Rotation)]?.ToObject<Vector3>() ?? new Vector3();
        return new PrefabPreviewPart(model, offset, new quat(rotation.ToRadiansGlm()));
    }

    /// <summary>
    /// A scenery prefab's meshes and LODs around the drop point, read for a scenery nobody has like its picture
    /// </summary>
    public static List<SceneryPlacement> ReadScenery(Prefab prefab)
    {
        if (prefab.Kind != PrefabKind.Scenery || PrefabLibrary.ModelOf(prefab) is not { } model)
        {
            return [];
        }

        var owner = new TT_Lab.Assets.Instance.Scenery { Package = new LabURI(prefab.Package), InvariantName = "Prefab preview", Alias = "Prefab preview", Chunk = string.Empty };
        using var stream = new MemoryStream(model);
        return new SceneryData(owner).ReadPlacements(TlmFile.Read(stream), System.Numerics.Vector3.Zero);
    }

    // A node's children go by their names, every part gets its own
    public void Show(RenderContext context, Renderable scene, vec3 at, IReadOnlyList<SceneryPlacement> placements)
    {
        _root = new Node(context, null, "PREFAB_PREVIEW") { DrawsAsPreview = true };
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            if (part.Model == null || Visual(context, part, $"PREFAB_PREVIEW_PART_{i}") is not { } visual)
            {
                _markers.Add(part.Offset);
                continue;
            }

            Add(visual);
        }

        for (var i = 0; i < placements.Count; i++)
        {
            if (SceneryData.CreatePlacementVisual(context, placements[i], $"PREFAB_PREVIEW_MESH_{i}") is { } visual)
            {
                Add(visual);
            }
        }

        // Nothing to draw it with still shows where it goes
        if (_shown.Count == 0 && _markers.Count == 0)
        {
            _markers.Add(vec3.Zero);
        }

        _root.Diffuse = Tint;
        scene.AddChild(_root);
        MoveTo(at);
    }

    // A model that doesn't read still shows where its instance goes
    private static EditableObject? Visual(RenderContext context, PrefabPreviewPart part, string name)
    {
        try
        {
            var visual = ObjectInstanceData.CreateVisual(context, part.Model!, name);
            visual.SetPosition(part.Offset);
            visual.SetRotation(part.Rotation);
            return visual;
        }
        catch (Exception e)
        {
            Log.WriteLine($"Model {part.Model} couldn't be drawn for a prefab's preview: {e.Message}", Log.LogType.Debug);
            return null;
        }
    }

    private void Add(EditableObject visual)
    {
        _root!.AddChild(visual);
        _shown.Add(visual);
    }

    public void MoveTo(vec3 at)
    {
        _at = at;
        _root?.SetPosition(at);
    }

    public void Remove(Renderable scene)
    {
        if (_root != null)
        {
            scene.RemoveChild(_root);
        }

        _root = null;
    }

    public void Draw(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (_root == null)
        {
            return;
        }

        foreach (var shown in _shown)
        {
            renderer.DrawWireBox(shown.GetBoundsTransform(), OutlineColor, 2.0f, PrimitiveLayer.WorldXRay);
        }

        foreach (var marker in _markers)
        {
            renderer.DrawWireBox(mat4.Translate(_at + marker) * mat4.Scale(MarkerHalfSize), OutlineColor, 2.0f, PrimitiveLayer.WorldXRay);
        }

        renderer.DrawCircle(_at, vec3.UnitY, camera.WorldUnitsPerPixel(_at) * RingPixels, OutlineColor, 2.0f, PrimitiveLayer.Overlay);
    }
}
