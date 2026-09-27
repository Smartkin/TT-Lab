using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Mesh = TT_Lab.Assets.Graphics.Mesh;

namespace TT_Lab.AssetData.Instance;

public class DynamicSceneryData : AbstractAssetData
{
    public DynamicSceneryData(IAsset asset) : base(asset)
    {
        DynamicModels = [];
    }

    public DynamicSceneryData(IAsset asset, ITwinDynamicScenery dynamicScenery) : this(asset)
    {
        SetTwinItem(dynamicScenery);
    }

    public List<DynamicSceneryModelData> DynamicModels { get; set; }

    public const string TlmAssetType = "DynamicScenery";
    public const string TlmKind = "dynamic_scenery";

    /// <summary>
    /// The dynamic scenery as a node holding every dynamic model
    /// </summary>
    public JsonObject WriteTlmNode(TlmFile file, TlmMaterials materials)
    {
        var node = TlmNodes.Create(TlmKind, "Dynamic Scenery");
        for (var i = 0; i < DynamicModels.Count; i++)
        {
            node.AddChild(DynamicModels[i].WriteTlmNode(file, materials, i));
        }

        return node;
    }

    /// <summary>
    /// Reads the dynamic models from their nodes, in the order they were written in
    /// </summary>
    public void ReadTlmNodes(TlmFile file, TlmMaterials materials, IEnumerable<TlmTreeNode> nodes)
    {
        DynamicModels = [];
        var ordered = nodes.Select((node, index) => (Node: node, Order: node.HasData ? node.Data.GetInt("Order", Int32.MaxValue) : Int32.MaxValue, Index: index))
            .OrderBy(n => n.Order).ThenBy(n => n.Index);
        foreach (var (node, _, index) in ordered)
        {
            var dynamicModel = DynamicSceneryModelData.FromTlm(file, node);
            dynamicModel.Mesh = node.Mesh != null ? RigidModelData.ReadTlm<Mesh>(Owner, file, node.Mesh, materials, null, $"DynamicModel_{index}").URI : LabURI.Empty;
            DynamicModels.Add(dynamicModel);
        }
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        var materials = new TlmMaterials(file, Owner);
        ReadTlmNodes(file, materials, file.Root == null ? [] : TlmTreeNode.Of(file.Root).Traverse().Where(node => node.Kind == DynamicSceneryModelData.TlmKind));
        DisposedValue = false;
        if (materials.AddedToProject)
        {
            SaveInternal(dataPath, settings);
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        DynamicModels.Clear();
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        file.Root = WriteTlmNode(file, new TlmMaterials(file));
        file.Save(dataPath);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var dynamicScenery = GetTwinItem<ITwinDynamicScenery>();
        DynamicModels.Clear();
        foreach (var model in dynamicScenery.DynamicModels)
        {
            DynamicModels.Add(new DynamicSceneryModelData(Owner, model));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(65545); // Dynamic scenery header
        writer.Write((Int16)DynamicModels.Count);
        foreach (var model in DynamicModels)
        {
            model.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateDynamicScenery(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);

        foreach (var model in DynamicModels)
        {
            assetManager.GetAsset(model.Mesh).ResolveChunkResources(factory, meshSection);
        }
        
        var item = base.ResolveChunkResources(factory, section, id, layoutId);
        item?.SetID(id);

        return item;
    }
}