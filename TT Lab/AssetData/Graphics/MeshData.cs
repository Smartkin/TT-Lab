using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SharpGLTF.Schema2;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Mesh = TT_Lab.Assets.Graphics.Mesh;

namespace TT_Lab.AssetData.Graphics;

public class MeshData : RigidModelData
{
    public MeshData(IAsset asset) : base(asset)
    {
    }

    public MeshData(IAsset asset, ITwinMesh mesh) : base(asset, mesh)
    {
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(260); // Unused header
        writer.Write(Materials.Count);
        foreach (var mat in Materials)
        {
            writer.Write(assetManager.GetAsset(mat).ExportTwinID);
        }
        writer.Write(assetManager.GetAsset(Model).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateMesh(ms);
    }
    
    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var model = ModelRoot.Load(dataPath);
        var rigidModelRoot = model.DefaultScene.VisualChildren.FirstOrDefault(n => n.Name.Contains("RIGID_MODEL_ROOT"));
        if (rigidModelRoot == null)
        {
            Log.WriteLine($"Misconfigured Mesh {dataPath}! Make sure it contains RIGID_MODEL_ROOT node!", Log.LogType.Error);
            return;
        }

        var rigidModel = ImportGltf<Mesh>(Owner, model, rigidModelRoot);
        var data = (MeshData)rigidModel.GetData();
        Model = data.Model;
        Materials.Clear();
        foreach (var material in data.Materials)
        {
            Materials.Add(material);
        }
    }

    protected override void ResolveResources(ITwinItemFactory factory, ITwinSection section)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var modelsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);

        foreach (var material in Materials)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(Model).ResolveChunkResources(factory, modelsSection);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id, int? layoutID = null)
    {
        return base.ResolveChunkResources(factory, section.GetParent() == null
            ? section.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION)
            : section, id, layoutID);
    }
}