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
            Links.Add(new ChunkLink(l));
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
        var assetManager = AssetManager.Get();
        var linkIdx = 0;
        foreach (var link in Links)
        {
            var linkedChunk = assetManager.GetAsset<LevelChunk>(link.Path);
            var chunkData = linkedChunk.ChunkResources;
            var linkedSceneryUri = chunkData.First(uri => assetManager.GetAsset(uri).Section == Constants.SCENERY_SECENERY_ITEM);
            var linkedScenery = assetManager.GetAssetData<SceneryData>(linkedSceneryUri);
            var linkedSceneryRender = new Rendering.Objects.Scenery(viewportContext.RenderContext, viewportContext.RenderContext.MeshService, linkedScenery);
            if (link is { IsAlwaysVisible: false, IsVisibleInCameraFrustum: false })
            {
                linkedSceneryRender.IsVisible = false;
            }

            var size = vec3.Ones;
            var offset = -vec3.Ones * 0.5f;
            var editableObject = new EditableObject(viewportContext.RenderContext, linkedSceneryRender, $"{Owner.FullDataPath}{linkIdx}", offset, size);
            editableObject.Init();
            editableObject.SetLocalTransform(link.ChunkMatrix.ToGlm());
            var billboard = viewportContext.EditingContext.CreateChunkLinkBillboard();
            editableObject.AddChild(billboard);

            var linkTransformProperty = property.Find($"[data].AssetData.Links[{linkIdx++}].{nameof(ChunkLink.ChunkMatrix)}")!;
            viewportObjects.Add(new ViewportObject(editableObject, $"CHUNK_LINK_{linkTransformProperty.Path}", property)
            {
                Transform = linkTransformProperty
            });
        }
        return viewportObjects;
    }
}