using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetData.Instance;

public class ParticleData : AbstractAssetData
{
    public ParticleData(IAsset asset) : base(asset)
    {
        ParticleSystems = [];
        ParticleInstances = [];
    }

    public ParticleData(IAsset asset, ITwinParticle particleData) : this(asset)
    {
        SetTwinItem(particleData);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentViewModel.EditorExplicitOrder, -10)]
    public List<ParticleSystem> ParticleSystems { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentViewModel.EditorExplicitOrder, -10)]
    public List<ParticleSystemInstance> ParticleInstances { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        ParticleSystems.Clear();
        ParticleInstances.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var particleData = GetTwinItem<ITwinParticle>();
        foreach (var twinSys in particleData.ParticleSystems)
        {
            ParticleSystems.Add(new ParticleSystem(twinSys));
        }

        foreach (var twinInst in particleData.ParticleEmitters)
        {
            ParticleInstances.Add(new ParticleSystemInstance(twinInst));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteExport(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateParticle(ms);
    }

    protected void WriteExport(BinaryWriter writer)
    {
        writer.Write(0x1E);
        writer.Write(ParticleSystems.Count);
        foreach (var system in ParticleSystems)
        {
            system.Write(writer);
        }

        writer.Write(ParticleInstances.Count);
        foreach (var emitter in ParticleInstances)
        {
            emitter.Write(writer);
        }
    }
}