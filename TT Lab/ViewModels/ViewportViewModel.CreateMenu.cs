using System;
using System.Collections.Generic;
using System.Linq;
using Splat;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.Extensions;
using TT_Lab.AssetData.Instance.Particle;
using GlmSharp;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Project.Prefabs;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels;

public sealed record ViewportMenuEntry(string Header, Action? Action = null, bool IsEnabled = true, bool IsSeparator = false)
{
    public static readonly ViewportMenuEntry Separator = new(string.Empty, IsSeparator: true);
}

// The menu a right click on the scene opens: instances to make at the cursor, put where the click hit the collision
public partial class ViewportViewModel
{
    private const string WumpaName = "WUMPA";

    internal IReadOnlyList<ViewportMenuEntry> GetCreateMenu(float x, float y)
    {
        if (!_isChunkViewport || _document?.DocumentModel is not LevelChunk chunk || _editingContext == null || _renderContext == null || IsFlying)
        {
            return [];
        }

        // The scenery mode's placeholders can go on the meshes as well
        if (IsSceneryMode ? TryHitScenery(x, y, out var hit) : TryHitCollision(x, y, out hit))
        {
            _editingContext.SetCursorCoordinates(hit);
        }

        var canCreate = _editingContext.IsCursorPlaced;
        if (IsSceneryMode)
        {
            var sceneryEntries = GetSceneryMenu(canCreate).ToList();
            if (!canCreate)
            {
                sceneryEntries.Insert(0, new ViewportMenuEntry("Click the collision to place the cursor first", IsEnabled: false));
            }

            return sceneryEntries;
        }

        var defaultObject = DefaultObjectFor(chunk);
        var aiPositions = SelectedAiPositions();
        var aiPaths = SelectedAiPaths();
        var hasLinks = FindResourceData(typeof(ChunkLinks)) != null;
        var hasParticles = FindResourceData(typeof(Particles)) != null;
        var hasScenery = FindResourceData(typeof(Scenery)) != null;
        ViewportMenuEntry Light(LightType type, string header) =>
            new(hasScenery ? header : $"{header} (the chunk has no scenery)", () => CreateLight(type), canCreate && hasScenery);
        ViewportMenuEntry Create(Type type, string header) => new(header, () => CreateResource(type, DefaultLayoutFor(type, chunk)), canCreate);
        var entries = new List<ViewportMenuEntry>
        {
            new(defaultObject == null ? "Object instance (the chunk has no objects)" : $"Object instance",
                () => CreateResource(typeof(ObjectInstance), DefaultLayoutFor(typeof(ObjectInstance), chunk)), canCreate && defaultObject != null),
            Create(typeof(Position), "Position"),
            Create(typeof(Trigger), "Trigger"),
            Create(typeof(Camera), "Camera"),
            Create(typeof(AiPosition), "AI position"),
            new(aiPositions.Count == 2 ? $"AI path from {aiPositions[0].Alias} to {aiPositions[1].Alias}" : "AI path (select two AI positions first, Shift+click)",
                () => CreateAiPath(aiPositions[0], aiPositions[1]), aiPositions.Count == 2),
            new(aiPaths.Count > 1 ? $"Reverse the {aiPaths.Count} selected AI paths" : "Reverse the selected AI path", () => ReverseAiPaths(aiPaths), aiPaths.Count > 0),
            Create(typeof(Path), "Path"),
            new(hasLinks ? "Chunk link" : "Chunk link (the chunk has no links resource)", () => CreateChunkLink(), canCreate && hasLinks),
            new(hasParticles ? "Particle emitter" : "Particle emitter (the chunk has no particles resource)", () => CreateParticleEmitter(), canCreate && hasParticles),
            ViewportMenuEntry.Separator,
            Light(LightType.Point, "Point light"),
            Light(LightType.Spot, "Spot light"),
            Light(LightType.Directional, "Directional light"),
            Light(LightType.Ambient, "Ambient light"),
            ViewportMenuEntry.Separator,
            new("Select every instance (Ctrl+A)", SelectAll),
        };
        if (!canCreate)
        {
            entries.Insert(0, new ViewportMenuEntry("Click the collision to place the cursor first", IsEnabled: false));
        }

        return entries;
    }

    // A right click while flying the camera is a look around, not a click for the menu
    private bool IsFlying => _keyboard != null && (_keyboard.IsKeyPressed(Silk.NET.Input.Key.W) || _keyboard.IsKeyPressed(Silk.NET.Input.Key.A)
                                                   || _keyboard.IsKeyPressed(Silk.NET.Input.Key.S) || _keyboard.IsKeyPressed(Silk.NET.Input.Key.D)
                                                   || _keyboard.IsKeyPressed(Silk.NET.Input.Key.Space) || _keyboard.IsKeyPressed(Silk.NET.Input.Key.C));

    /// <summary>
    /// The object new object instances get: the game's wumpa fruit pickup, or failing that any object named after it, or the chunk's first
    /// </summary>
    internal static GameObject? DefaultObjectFor(LevelChunk chunk)
    {
        var objects = AssetManager.Get().GetRelatedAssetsOf<GameObject>(chunk.Package);
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project.Project;
        bool IsGlobal(GameObject gameObject) => project != null && (gameObject.Package == project.GlobalPackagePS2.URI || gameObject.Package == project.GlobalPackageXbox.URI);
        return objects.FirstOrDefault(gameObject => gameObject.InvariantName.Equals(WumpaName, StringComparison.OrdinalIgnoreCase))
               ?? objects.Where(gameObject => gameObject.InvariantName.Contains(WumpaName, StringComparison.OrdinalIgnoreCase))
                   .OrderBy(gameObject => IsGlobal(gameObject) ? 0 : 1).ThenBy(gameObject => gameObject.InvariantName.Length).ThenBy(gameObject => gameObject.ID)
                   .FirstOrDefault()
               ?? objects.OrderBy(gameObject => IsGlobal(gameObject) ? 0 : 1).ThenBy(gameObject => gameObject.ID).FirstOrDefault();
    }

    // Retail paths have 4 points or more
    private const int NewPathPoints = 4;
    private const float NewPathSpacing = 2.0f;
    private const float NewResourceDistance = 10.0f;

    /// <summary>
    /// Where new resources go: at the cursor, in front of the camera while the cursor isn't placed
    /// </summary>
    internal vec3 NewResourcePosition()
    {
        if (_editingContext is { IsCursorPlaced: true })
        {
            return _editingContext.GetCursorCoordinates();
        }

        if (_scene == null)
        {
            return vec3.Zero;
        }

        var camera = _scene.Camera.GetFrameCamera();
        return camera.Position + camera.Forward * NewResourceDistance;
    }

    /// <summary>
    /// The layout the chunk keeps most of the type's instances in, else the one the game's chunks keep them in
    /// </summary>
    internal static int DefaultLayoutFor(Type type, LevelChunk chunk)
    {
        var assetManager = AssetManager.Get();
        var used = chunk.ChunkResources.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset)
            .Where(asset => asset.GetType() == type && asset.LayoutID != null)
            .GroupBy(asset => asset.LayoutID!.Value)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
            .FirstOrDefault();
        if (used != null)
        {
            return used.Key;
        }

        if (type == typeof(Camera))
        {
            return 4;
        }

        if (type == typeof(AiPosition) || type == typeof(AiPath))
        {
            return 6;
        }

        return type == typeof(CollisionSurface) ? ChunkLayouts.CollisionSurfaces : 0;
    }

    /// <summary>
    /// Types of instances a new one of can be made in a chunk's layout, AI paths join two selected AI positions
    /// </summary>
    internal static readonly IReadOnlyList<Type> CreatableTypes =
        [typeof(ObjectInstance), typeof(Position), typeof(Trigger), typeof(Camera), typeof(AiPosition), typeof(AiPath), typeof(Path), typeof(InstanceTemplate), typeof(CollisionSurface)];

    /// <summary>
    /// A new instance of the type in the chunk's layout, at the cursor when it has a place, selected and one step to undo. None when it
    /// can't be made: an object instance without any object, an AI path without two AI positions selected
    /// </summary>
    internal IAsset? CreateResource(Type type, int layout)
    {
        if (_document?.DocumentModel is not LevelChunk chunk)
        {
            return null;
        }

        var position = NewResourcePosition();
        var displayName = PrefabLibrary.Describe(type);
        IAsset instance;
        vec3? placeAt = position;
        if (type == typeof(ObjectInstance))
        {
            if (DefaultObjectFor(chunk) is not { } gameObject)
            {
                Log.WriteLine("The chunk has no game objects to make an instance of", Log.LogType.Warning);
                return null;
            }

            return CreateObjectInstance(gameObject, layout);
        }

        if (type == typeof(AiPath))
        {
            var aiPositions = SelectedAiPositions();
            if (aiPositions.Count != 2)
            {
                Log.WriteLine("An AI path joins two AI positions, select them first", Log.LogType.Warning);
                return null;
            }

            return CreateAiPath(aiPositions[0], aiPositions[1]);
        }

        if (type == typeof(Path))
        {
            // Its points are its place, placing moves every object of the new instance to the spot
            instance = CreateInstance(type, NewName(displayName), (Enums.Layouts)layout, asset =>
            {
                asset.SetData(new PathData(asset)
                {
                    Points = Enumerable.Range(0, NewPathPoints)
                        .Select(point => new Twinsanity.TwinsanityInterchange.Common.Vector3(position.x + point * NewPathSpacing, position.y, position.z)).ToList(),
                });
                return AssetCreationStatus.Success;
            });
            placeAt = null;
        }
        else if (CreatableTypes.Contains(type))
        {
            instance = CreateInstance(type, NewName(displayName), (Enums.Layouts)layout, CreatorOf(type));
            if (type == typeof(InstanceTemplate) || type == typeof(CollisionSurface))
            {
                placeAt = null;
            }
        }
        else
        {
            return null;
        }

        PlaceInstances([(instance, placeAt)], $"Placed {displayName}");
        return instance;
    }

    /// <summary>
    /// A new instance of the object in the chunk's layout at the cursor, with what the object's type needs (its template values, those of
    /// its type without any), selected and one step to undo
    /// </summary>
    internal IAsset CreateObjectInstance(GameObject gameObject, int layout)
    {
        var displayName = $"{gameObject.Alias} instance";
        var instance = CreateInstance(typeof(ObjectInstance), NewName(displayName), (Enums.Layouts)layout, asset =>
        {
            var status = AssetDataFactory.CreateObjectInstanceData(asset);
            var data = asset.GetData<ObjectInstanceData>();
            data.ObjectId = gameObject.URI;
            data.TakeValuesOf((GameObjectData)gameObject.GetData());
            return status;
        });
        PlaceInstances([(instance, NewResourcePosition())], $"Placed {displayName}");
        return instance;
    }

    /// <summary>
    /// Whether an instance of the object can go into the chunk: an object of its version of the game, which its own and the related
    /// packages have
    /// </summary>
    internal bool CanPlaceObject(GameObject gameObject)
    {
        return _document?.DocumentModel is LevelChunk chunk && AssetManager.Get().IsRelated(chunk.Package, gameObject.Package);
    }

    /// <summary>
    /// An instance of the object dragged from the project tree, where the ray through the viewport point hits the chunk's collision, else at
    /// the cursor, in the layout the chunk keeps its object instances in
    /// </summary>
    internal IAsset? PlaceObjectAt(GameObject gameObject, float x, float y)
    {
        HidePrefabPreview();
        if (_document?.DocumentModel is not LevelChunk chunk || !CanPlaceObject(gameObject))
        {
            return null;
        }

        if (_editingContext != null && TryHitCollision(x, y, out var hit))
        {
            _editingContext.SetCursorCoordinates(hit);
        }

        return CreateObjectInstance(gameObject, DefaultLayoutFor(typeof(ObjectInstance), chunk));
    }

    // The object dragged over the scene is shown where letting it go puts its instance, like a prefab of an instance of it
    private Prefab? _draggedObject;

    internal void ShowObjectPreview(GameObject gameObject, float x, float y)
    {
        if (_draggedObject?.Data[nameof(ObjectInstanceData.ObjectId)]?.ToObject<LabURI>() != gameObject.URI)
        {
            _draggedObject = new Prefab
            {
                Kind = PrefabKind.Instance,
                Name = gameObject.Alias,
                AssetType = typeof(ObjectInstance).FullName!,
                Data = new JObject { [nameof(ObjectInstanceData.ObjectId)] = JToken.FromObject(gameObject.URI) },
            };
        }

        ShowPrefabPreview(_draggedObject, x, y);
    }

    private static string NewName(string displayName) => $"New {displayName} {(uint)Guid.NewGuid().GetHashCode():X8}";

    private static Func<IAsset, AssetCreationStatus> CreatorOf(Type type)
    {
        if (type == typeof(Position))
        {
            return AssetDataFactory.CreatePositionData;
        }

        if (type == typeof(Trigger))
        {
            return AssetDataFactory.CreateTriggerData;
        }

        if (type == typeof(Camera))
        {
            return AssetDataFactory.CreateCameraData;
        }

        if (type == typeof(AiPosition))
        {
            return AssetDataFactory.CreateAiPositionData;
        }

        if (type == typeof(InstanceTemplate))
        {
            return AssetDataFactory.CreateInstanceTemplateData;
        }

        if (type == typeof(CollisionSurface))
        {
            return AssetDataFactory.CreateCollisionSurfaceData;
        }

        throw new ArgumentException($"{type.Name} instances aren't made from nothing", nameof(type));
    }

    // The AI positions selected, in the order they got selected: an AI path joins two of them
    internal List<AiPosition> SelectedAiPositions()
    {
        return SelectedObjects.Select(viewportObject => viewportObject.Property.Find("[data]")?.GetValue()).OfType<AiPosition>().Distinct().ToList();
    }

    // The AI paths selected
    internal List<ViewportObject> SelectedAiPaths()
    {
        return SelectedObjects.Where(viewportObject => viewportObject.Property.Find("[data]")?.GetValue() is AiPath).Distinct().ToList();
    }

    /// <summary>
    /// Swaps the positions every AI path joins, so it goes the other way, in one step
    /// </summary>
    internal void ReverseAiPaths(IReadOnlyList<ViewportObject> paths)
    {
        using var step = _document!.History.BeginGroup(paths.Count > 1 ? $"Reversed {paths.Count} AI paths" : "Reversed an AI path");
        foreach (var path in paths)
        {
            var begin = path.Property.Find($"[data].AssetData.{nameof(AiPathData.PathBegin)}");
            var end = path.Property.Find($"[data].AssetData.{nameof(AiPathData.PathEnd)}");
            if (begin == null || end == null)
            {
                continue;
            }

            var from = begin.GetValue();
            begin.SetValue(end.GetValue());
            end.SetValue(from);
        }
    }

    /// <summary>
    /// A new AI path of the chunk from one AI position to another, in the layout of the first
    /// </summary>
    internal IAsset CreateAiPath(AiPosition from, AiPosition to)
    {
        var name = $"AI path {from.Alias} to {to.Alias} {(uint)Guid.NewGuid().GetHashCode():X8}";
        var path = CreateInstance(typeof(AiPath), name, (Enums.Layouts)(from.LayoutID ?? 0), asset =>
        {
            asset.SetData(new AiPathData(asset) { PathBegin = from.URI, PathEnd = to.URI });
            return AssetCreationStatus.Success;
        });
        PlaceInstances([(path, null)], $"Placed AI path {from.Alias} to {to.Alias}");
        return path;
    }

    /// <summary>
    /// A new link of the chunk at the cursor, to the first other chunk of its package until another is picked, its load wall standing on
    /// the cursor
    /// </summary>
    internal PropertyNode? CreateChunkLink()
    {
        var chunk = (LevelChunk)_document!.DocumentModel;
        var assetManager = AssetManager.Get();
        var target = assetManager.GetRelatedAssetsOf<LevelChunk>(chunk.Package)
            .Where(other => other.URI != chunk.URI && other.Name != "default")
            .OrderBy(other => other.Package == chunk.Package ? 0 : 1).ThenBy(other => other.URI.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();
        var position = NewResourcePosition();
        var at = mat4.Translate(position).ToTwin();
        var link = new ChunkLink { Path = target?.URI ?? LabURI.Empty, ObjectMatrix = at, ChunkMatrix = at, LoadingWall = ChunkLink.WallAt(position) };
        return CreateElement(typeof(ChunkLinks), $"AssetData.{nameof(ChunkLinksData.Links)}", link, "Placed a chunk link");
    }

    /// <summary>
    /// A new emitter of the chunk at the cursor, playing the first system the chunk can use
    /// </summary>
    internal PropertyNode? CreateParticleEmitter()
    {
        var emitter = new ParticleSystemInstance();
        if (FindResourceData(typeof(Particles))?.GetValue() is Particles particles && ((IAsset)particles).GetData<ParticleData>().GetUsableSystems().FirstOrDefault().System is { } system)
        {
            emitter.Name = system.Name;
        }

        var cursor = NewResourcePosition();
        emitter.Position = new Twinsanity.TwinsanityInterchange.Common.Vector3(cursor.x, cursor.y, cursor.z);
        return CreateElement(typeof(Particles), $"AssetData.{nameof(ParticleData.ParticleInstances)}", emitter, "Placed a particle emitter");
    }

    /// <summary>
    /// A new light of the chunk's scenery at the cursor, like the ones new chunks get, which lights the chunk when it had none
    /// </summary>
    internal PropertyNode? CreateLight(LightType type)
    {
        (Light Light, string List, string Name) made = type switch
        {
            LightType.Ambient => (DefaultLights.Ambient(), nameof(SceneryData.AmbientLights), "an ambient light"),
            LightType.Directional => (DefaultLights.Directional(), nameof(SceneryData.DirectionalLights), "a directional light"),
            LightType.Spot => (DefaultLights.Spot(), nameof(SceneryData.SpotLights), "a spot light"),
            _ => (DefaultLights.Point(), nameof(SceneryData.PointLights), "a point light"),
        };
        var cursor = NewResourcePosition();
        made.Light.Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(cursor.x, cursor.y, cursor.z, 1.0f);
        made.Light.ComputeBounds();
        var description = $"Placed {made.Name}";
        return CreateElement(typeof(Scenery), $"AssetData.{made.List}", made.Light, description);
    }

}
