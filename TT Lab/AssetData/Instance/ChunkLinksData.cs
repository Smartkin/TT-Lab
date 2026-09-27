using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Scene;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class ChunkLinksData : AbstractAssetData
{
    public ChunkLinksData(IAsset asset) : base(asset)
    {
    }

    public ChunkLinksData(IAsset asset, ITwinLink link) : this(asset)
    {
        SetTwinItem(link);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorCollectionItemPrefix("Chunk Link")]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<ChunkLink> Links { get; set; } = new();

    protected override void Dispose(Boolean disposing)
    {
        Links.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var link = GetTwinItem<ITwinLink>();
        foreach (var l in link.LinksList)
        {
            Links.Add(new ChunkLink(l, Owner.Package));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(Links.Count);
        foreach (var link in Links)
        {
            writer.Write(link.UnkFlag);
            writer.Write(assetManager.GetAsset<LevelChunk>(link.Path).GetChunkPath().ToLowerInvariant());
            writer.Write(link.IsAlwaysVisible);
            writer.Write(link.IsVisibleInCameraFrustum);
            writer.Write(link.UnkNum);
            writer.Write(link.IsLoadWallActive);
            writer.Write(link.KeepLoaded);
            link.ObjectMatrix.Write(writer);
            link.ChunkMatrix.Write(writer);
            
            var hasValidLoadWall = link.LoadingWall.ToGlm() != mat4.Zero;
            writer.Write(hasValidLoadWall);
            if (hasValidLoadWall)
            {
                link.LoadingWall.Write(writer);
            }

            writer.Write(link.ChunkLinksCollisionData.Count);
            foreach (var collisionData in link.ChunkLinksCollisionData)
            {
                collisionData.Write(writer);
            }
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateLink(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var viewportObjects = new List<ViewportObject>();
        var linksProperty = property.Find($"[data].AssetData.{nameof(Links)}");
        if (linksProperty == null)
        {
            return viewportObjects;
        }

        for (var linkIdx = 0; linkIdx < Links.Count; linkIdx++)
        {
            var linkProperty = property.Find($"[data].AssetData.{nameof(Links)}[{linkIdx}]")!;
            var linkedScenery = CreateLinkedScenery(viewportContext, property, linksProperty, linkProperty, linkIdx);
            if (linkedScenery != null)
            {
                viewportObjects.Add(linkedScenery);
            }

            var loadWall = CreateLoadWall(viewportContext, property, linksProperty, linkProperty, linkIdx);
            if (loadWall != null)
            {
                viewportObjects.Add(loadWall);
            }
        }

        return viewportObjects;
    }

    private ViewportObject? CreateLinkedScenery(ViewportContext viewportContext, PropertyNode property, PropertyNode linksProperty, PropertyNode linkProperty, int linkIdx)
    {
        var link = Links[linkIdx];
        var assetManager = AssetManager.Get();
        if (link.Path == LabURI.Empty || !assetManager.DoesAssetExist(link.Path))
        {
            return null;
        }

        var linkedChunk = assetManager.GetAsset<LevelChunk>(link.Path);
        var linkedSceneryUri = linkedChunk.ChunkResources.FirstOrDefault(uri => assetManager.GetAsset(uri).Section == Constants.SCENERY_SECENERY_ITEM);
        if (linkedSceneryUri == null)
        {
            return null;
        }

        var linkedScenery = assetManager.GetAssetData<SceneryData>(linkedSceneryUri);
        var linkedSceneryRender = new Rendering.Objects.Scenery(viewportContext.RenderContext, viewportContext.RenderContext.MeshService, linkedScenery);
        linkedSceneryRender.IsVisible = IsLinkedSceneryShown(link);

        var size = vec3.Ones;
        var offset = -vec3.Ones * 0.5f;
        var editableObject = new EditableObject(viewportContext.RenderContext, linkedSceneryRender, $"{Owner.FullDataPath}{linkIdx}", offset, size);
        editableObject.Init();
        editableObject.SetLocalTransform(link.ChunkMatrix.ToGlm());
        var billboard = viewportContext.EditingContext.CreateChunkLinkBillboard();
        editableObject.AddChild(billboard);

        var linkTransformProperty = linkProperty.Find(nameof(ChunkLink.ChunkMatrix))!;
        var isRebuildNeeded = CreateRebuildCheck(link, linkIdx);
        return new ViewportObject(editableObject, $"CHUNK_LINK_{linkTransformProperty.Path}", property)
        {
            Transform = linkTransformProperty,
            Category = ViewportObjectCategory.LinkedScenery,
            InspectorFocus = linkProperty,
            DuplicatedElement = linkProperty,
            RenderDependencies = [linksProperty],
            Refresh = () =>
            {
                if (isRebuildNeeded())
                {
                    return false;
                }

                linkedSceneryRender.IsVisible = IsLinkedSceneryShown(link);
                return true;
            },
        };
    }

    private ViewportObject? CreateLoadWall(ViewportContext viewportContext, PropertyNode property, PropertyNode linksProperty, PropertyNode linkProperty, int linkIdx)
    {
        var link = Links[linkIdx];
        if (!LoadWallCorners.IsUsable(link.LoadingWall))
        {
            return null;
        }

        var corners = new LoadWallCorners();
        // A thin box around the wall to pick it by
        var editableObject = new EditableObject(viewportContext.RenderContext, null, $"{Owner.FullDataPath}{linkIdx}_LOAD_WALL", new vec3(-1.0f, -1.0f, -0.05f), new vec3(2.0f, 2.0f, 0.1f));
        editableObject.Init();
        editableObject.SetLocalTransform(corners.ToTransform(link.LoadingWall));
        var visual = new LoadWallVisual(viewportContext.RenderContext, editableObject, corners, link.IsLoadWallActive);

        var wallProperty = linkProperty.Find(nameof(ChunkLink.LoadingWall))!;
        var isRebuildNeeded = CreateRebuildCheck(link, linkIdx);
        return new ViewportObject(editableObject, $"LOAD_WALL_{wallProperty.Path}", property)
        {
            Transform = wallProperty,
            TransformConverter = corners,
            Category = ViewportObjectCategory.LoadWalls,
            InspectorFocus = wallProperty,
            // A load wall is its link's, a copy of the wall is a copy of the link
            DuplicatedElement = linkProperty,
            RenderDependencies = [linksProperty],
            Refresh = () =>
            {
                if (isRebuildNeeded())
                {
                    return false;
                }

                visual.IsActive = link.IsLoadWallActive;
                return true;
            },
        };
    }

    // Links coming or going move the others to other indices, another linked chunk has other scenery, and a wall that comes or goes adds or
    // removes an object
    private Func<bool> CreateRebuildCheck(ChunkLink link, int linkIdx)
    {
        var linkCount = Links.Count;
        var linkedPath = link.Path;
        var hasWall = LoadWallCorners.IsUsable(link.LoadingWall);
        return () => Links.Count != linkCount || Links[linkIdx] != link || link.Path != linkedPath || LoadWallCorners.IsUsable(link.LoadingWall) != hasWall;
    }

    private static bool IsLinkedSceneryShown(ChunkLink link)
    {
        return link.IsAlwaysVisible || link.IsVisibleInCameraFrustum;
    }
}