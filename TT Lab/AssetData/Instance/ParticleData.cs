using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetData.Instance;

public class ParticleData : AbstractAssetData
{
    // The game's tables of what the loaded chunks have (ReadMainParticleSection): systems past them are read and dropped, emitters
    // past them aren't read
    public const int MaxLoadedSystems = 300;
    public const int MaxLoadedEmitters = 0x300;

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
    [Editable(Hint = "The game keeps 300 particle systems at once, the default chunk's first (255 on the PS2) and then every loaded chunk's")]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -10)]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLoadedSystems)]
    public List<ParticleSystem> ParticleSystems { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -10)]
    [EditorHiddenWhen(nameof(CannotHaveEmitters))]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLoadedEmitters)]
    public List<ParticleSystemInstance> ParticleInstances { get; set; }

    // The default chunk's particles only hold the systems every chunk can use
    protected virtual bool CannotHaveEmitters => false;

    /// <summary>
    /// Systems the chunk's emitters can play, emitters name theirs. The chunk's own come before the default chunk's, a few names are in both
    /// </summary>
    public IEnumerable<(ParticleSystem System, bool IsDefault)> GetUsableSystems()
    {
        foreach (var system in ParticleSystems)
        {
            yield return (system, false);
        }

        if (this is DefaultParticleData)
        {
            yield break;
        }

        foreach (var defaults in AssetManager.Get().GetRelatedAssetsOf<DefaultParticles>(Owner.Package))
        {
            foreach (var system in ((IAsset)defaults).GetData<DefaultParticleData>().ParticleSystems)
            {
                yield return (system, true);
            }
        }
    }

    /// <summary>
    /// Textures of the pages particle systems take their pictures from, only the default chunk's particles have them
    /// </summary>
    public IReadOnlyList<LabURI> GetTexturePages()
    {
        if (this is DefaultParticleData defaultData)
        {
            return defaultData.TextureIDs;
        }

        var defaults = AssetManager.Get().GetRelatedAssetsOf<DefaultParticles>(Owner.Package).FirstOrDefault();
        return defaults == null ? [] : ((IAsset)defaults).GetData<DefaultParticleData>().TextureIDs;
    }

    /// <summary>
    /// The alpha blending of every page's material, the Page material blend mode draws the particles with it
    /// </summary>
    public IReadOnlyList<TwinShader.AlphaBlendPresets> GetPageBlends()
    {
        var defaults = this as DefaultParticleData;
        if (defaults == null)
        {
            var asset = AssetManager.Get().GetRelatedAssetsOf<DefaultParticles>(Owner.Package).FirstOrDefault();
            defaults = asset == null ? null : ((IAsset)asset).GetData<DefaultParticleData>();
        }

        if (defaults == null)
        {
            return [];
        }

        var assetManager = AssetManager.Get();
        return defaults.MaterialIDs.Select(uri => assetManager.DoesAssetExist(uri)
            ? assetManager.GetAssetData<MaterialData>(uri).Shaders.FirstOrDefault()?.AlphaRegSettingsIndex ?? TwinShader.AlphaBlendPresets.Mix
            : TwinShader.AlphaBlendPresets.Mix).ToList();
    }

    protected void CheckLoadedCounts()
    {
        CheckCount("particle systems", ParticleSystems.Count, MaxLoadedSystems);
        CheckCount("emitters", ParticleInstances.Count, MaxLoadedEmitters);
    }

    private int DefaultSystemCount()
    {
        return AssetManager.Get().GetRelatedAssetsOf<DefaultParticles>(Owner.Package).Select(defaults => ((IAsset)defaults).GetData<DefaultParticleData>().ParticleSystems.Count)
            .FirstOrDefault();
    }

    public (ParticleSystem System, bool IsDefault)? FindSystem(string name)
    {
        return GetUsableSystems().Where(usable => usable.System.Name == name).Select(usable => ((ParticleSystem, bool)?)usable).FirstOrDefault();
    }

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
        CheckLoadedCounts();
        if (this is not DefaultParticleData && DefaultSystemCount() is var defaults and > 0 && defaults + ParticleSystems.Count > MaxLoadedSystems)
        {
            Log.WriteLine($"{Owner.Alias} has {ParticleSystems.Count} particle systems and the default chunk {defaults}, the game keeps {MaxLoadedSystems} at once and drops the rest",
                Log.LogType.Warning);
        }

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
        
        var pages = GetTexturePages();
        var pageBlends = GetPageBlends();
        var instIdx = 0;
        foreach (var inst in ParticleInstances)
        {
            var visual = viewportContext.EditingContext.CreateParticleBillboard();
            var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
            visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
            // Same size as the billboard
            var size = vec3.Ones * 2.0f;
            var offset = -vec3.Ones;
            var editableObject = new EditableObject(viewportContext.RenderContext, visual, $"{Owner.FullDataPath}{instIdx}", offset, size);
            color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
            editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
            editableObject.UnselectedColor = visual.Diffuse;
            editableObject.SetPosition(inst.Position.ToGlm());

            var emitter = new ParticleEmitter(viewportContext.RenderContext, $"{Owner.FullDataPath}{instIdx} particles", pages, pageBlends, instIdx);
            emitter.Configure(FindSystem(inst.Name)?.System, inst);
            editableObject.AddChild(emitter);

            var particleProp = property.Find($"[data].AssetData.{nameof(ParticleInstances)}[{instIdx++}].{nameof(ParticleSystemInstance.Position)}")!;
            var systems = property.Find($"[data].AssetData.{nameof(ParticleSystems)}")!;
            var emitterData = inst;
            viewportObjects.Add(new ViewportObject(editableObject, particleProp.Path, property)
            {
                Position = particleProp,
                Category = ViewportObjectCategory.Particles,
                InspectorFocus = particleProp.Parent,
                DuplicatedElement = particleProp.Parent,
                // The emitter's system can be renamed or edited, or it can be pointed at another one
                RenderDependencies = [particleProp.Parent!, systems],
                Refresh = () =>
                {
                    emitter.Configure(FindSystem(emitterData.Name)?.System, emitterData);
                    return true;
                },
            });
        }
        
        return viewportObjects;
    }
}