using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

/// <summary>
/// A rigid model placed by scenery, LODs and skydomes
/// </summary>
public class MeshData : RigidModelData
{
    public new const string TlmAssetType = "Mesh";

    public MeshData(IAsset asset) : base(asset)
    {
    }

    public MeshData(IAsset asset, ITwinMesh mesh) : base(asset, mesh)
    {
    }

    protected override string AssetTlmType => TlmAssetType;
    protected override string TlmKind => "mesh";
    protected override int ExportHeader => 260;

    protected override ITwinItem CreateItem(ITwinItemFactory factory, Stream stream)
    {
        return factory.GenerateMesh(stream);
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

        assetManager.GetAsset<Model>(Model).ResolveChunkResources(factory, modelsSection);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        return base.ResolveChunkResources(factory, section.GetParent() == null
            ? section.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION)
            : section, id, layoutId);
    }
}
