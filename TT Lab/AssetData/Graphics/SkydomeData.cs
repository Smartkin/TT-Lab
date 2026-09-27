using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GlmSharp;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Mesh = TT_Lab.Assets.Graphics.Mesh;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class SkydomeData : AbstractAssetData
{
    public SkydomeData(IAsset asset) : base(asset)
    {
        Meshes = [];
    }

    public SkydomeData(IAsset asset, ITwinSkydome skydome) : this(asset)
    {
        SetTwinItem(skydome);
    }

    public List<LabURI> Meshes { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Meshes.Clear();
    }

    public const string TlmAssetType = "Skydome";
    public const string TlmKind = "skydome";
    public const string SkydomeMeshKind = "skydome_mesh";

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        var assetManager = AssetManager.Get();
        for (var i = 0; i < Meshes.Count; i++)
        {
            var node = root.AddChild(TlmNodes.Create(SkydomeMeshKind, $"Skydome Mesh {i}", new System.Text.Json.Nodes.JsonObject { ["Order"] = i }));
            node[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(Meshes[i]).WriteTlmMesh(file, materials);
        }

        file.Root = root;
        file.Save(dataPath);
    }

    // Every mesh is one of the skydome's meshes, drawn in the order they were written in
    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        var materials = new TlmMaterials(file, Owner);
        Meshes.Clear();
        var nodes = (file.Root == null ? [] : TlmTreeNode.Of(file.Root).Traverse().Where(node => node.Mesh != null))
            .Select((node, index) => (Node: node, Order: node.HasData ? node.Data.GetInt("Order", Int32.MaxValue) : Int32.MaxValue, Index: index))
            .OrderBy(n => n.Order).ThenBy(n => n.Index);
        foreach (var (node, _, index) in nodes)
        {
            Meshes.Add(RigidModelData.ReadTlm<Mesh>(Owner, file, node.Mesh!, materials, node.GetBakedTransform(), $"SkydomeMesh_{index}").URI);
        }

        DisposedValue = false;
        if (materials.AddedToProject)
        {
            SaveInternal(dataPath, settings);
        }
    }

    public override String GetStringified()
    {
        var result = new StringBuilder();
        foreach (var labUri in Meshes)
        {
            result.AppendLine(AssetManager.Get().GetAssetData<MeshData>(labUri).GetStringified());
        }
        return result.ToString();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var skydome = GetTwinItem<ITwinSkydome>();
        Meshes = new List<LabURI>();
        foreach (var mesh in skydome.Meshes)
        {
            Meshes.Add(AssetManager.Get().GetUriByTwinId<Mesh>(Owner, mesh));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(20480); // Unused header
        writer.Write(Meshes.Count);
        foreach (var mesh in Meshes)
        {
            writer.Write(assetManager.GetAsset(mesh).ExportTwinID);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateSkydome(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var root = section.GetRoot();
        var graphicsSection = root.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        foreach (var mesh in Meshes)
        {
            assetManager.GetAsset(mesh).ResolveChunkResources(factory, meshSection);
        }

        section = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var visual = new Skydome(viewportContext.RenderContext, this, viewportContext.RenderContext.MeshService);
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, "SKYDOME_EDITABLE")
        {
            IsSelectable = false
        };
        return [new ViewportObject(editableObject, $"SKYDOME_EDITABLE_{property.Path}", property) { Category = ViewportObjectCategory.Skydome }];
    }
}