using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class ChunkLinksData : AbstractAssetData
{
    public ChunkLinksData()
    {
    }

    public ChunkLinksData(ITwinLink link) : this()
    {
        SetTwinItem(link);
    }

    [JsonProperty(Required = Required.Always)]
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
            writer.Write(assetManager.GetAsset(link.Path).Variation[..^4].ToLowerInvariant());
            writer.Write(link.IsRendered);
            writer.Write(link.UnkNum);
            writer.Write(link.IsLoadWallActive);
            writer.Write(link.KeepLoaded);
            link.ObjectMatrix.Write(writer);
            link.ChunkMatrix.Write(writer);
            writer.Write(link.LoadingWall != null);
            link.LoadingWall?.Write(writer);
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
}