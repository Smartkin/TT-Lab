using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpGLTF.Schema2;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
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

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanitySkydome_{Owner.Name}");
        var root = new SharpGLTF.Scenes.NodeBuilder("SKYDOME_ROOT");

        var meshesRoot = root.CreateNode("SKYDOME_MESHES");
        var assetManager = AssetManager.Get();
        var meshIndex = 0;
        foreach (var meshId in Meshes)
        {
            var meshData = assetManager.GetAssetData<MeshData>(meshId);
            meshData.ExportGltf(scene, meshesRoot, meshIndex.ToString());
            meshIndex++;
        }
            
        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var model = ModelRoot.Load(dataPath);
        Meshes.Clear();
        var skydomeRoot = model.DefaultScene.VisualChildren.FirstOrDefault(n => n.Name.Contains("SKYDOME_ROOT"));
        if (skydomeRoot == null)
        {
            Log.WriteLine($"Misconfigured Skydome {dataPath}! Make sure it contains SKYDOME_ROOT node!", Log.LogType.Error);
            return;
        }
        var meshesNode = skydomeRoot.VisualChildren.FirstOrDefault(n => n.Name.Contains("SKYDOME_MESHES"));
        if (meshesNode == null)
        {
            Log.WriteLine($"Misconfigured Skydome {dataPath}! Make sure SKYDOME_ROOT contains SKYDOME_MESHES node!");
            return;
        }

        foreach (var meshNode in meshesNode.VisualChildren)
        {
            var mesh = RigidModelData.ImportGltf<Mesh>(Owner, model, meshNode);
            Meshes.Add(mesh.URI);
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

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
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
        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}