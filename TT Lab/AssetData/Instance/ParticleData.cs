using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
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
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -10)]
    public List<ParticleSystem> ParticleSystems { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -10)]
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

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var viewportObjects = new List<ViewportObject>();
        if (ParticleInstances.Count == 0)
        {
            return viewportObjects;
        }
        
        var instIdx = 0;
        foreach (var inst in ParticleInstances)
        {
            var visual = viewportContext.EditingContext.CreateParticleBillboard();
            var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
            visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
            var size = vec3.Ones;
            var offset = -vec3.Ones * 0.5f;
            var editableObject = new EditableObject(viewportContext.RenderContext, visual, $"{Owner.FullDataPath}{instIdx}", offset, size);
            color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
            editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
            editableObject.UnselectedColor = visual.Diffuse;
            editableObject.SetPosition(inst.Position.ToGlm());

            var particleProp = property.Find($"[data].AssetData.{nameof(ParticleInstances)}[{instIdx++}].{nameof(ParticleSystemInstance.Position)}")!;
            viewportObjects.Add(new ViewportObject(editableObject, particleProp.Path, property)
            {
                Position = particleProp
            });
        }
        
        return viewportObjects;
    }
}