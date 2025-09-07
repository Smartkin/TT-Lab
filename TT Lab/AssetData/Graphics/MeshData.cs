using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics
{
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
                writer.Write(assetManager.GetAsset(mat).ID);
            }
            writer.Write(assetManager.GetAsset(Model).ID);

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateMesh(ms);
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
    }
}
