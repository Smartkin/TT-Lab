using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.AssetData;

public class LevelChunkData(IAsset owner) : AbstractAssetData(owner)
{
    protected override void Dispose(bool disposing)
    {
    }

    public override void Import(LabURI package, string? variant, int? layoutId)
    {
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        return factory.GenerateRM();
    }
}