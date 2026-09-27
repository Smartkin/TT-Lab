using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class DefaultParticleData : ParticleData
{

    public DefaultParticleData(IAsset asset) : base(asset)
    {
        UnkData = new Byte[4];
        UnkBlob = new Byte[0x420];
        UnkInts = new Int32[10];
    }

    public DefaultParticleData(IAsset asset, ITwinDefaultParticle particle) : base(asset, particle) { }

    protected override bool CannotHaveEmitters => true;

    private const float PreviewSpacing = 10.0f;

    /// <summary>
    /// The default chunk has nothing else to show, every system plays on a grid in front of the camera to be looked at
    /// </summary>
    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var viewportObjects = new List<ViewportObject>();
        var systems = property.Find($"[data].AssetData.{nameof(ParticleSystems)}");
        if (systems == null)
        {
            return viewportObjects;
        }

        var pages = GetTexturePages();
        var columns = Math.Max((int)MathF.Ceiling(MathF.Sqrt(ParticleSystems.Count)), 1);
        var still = new ParticleSystemInstance();
        for (var i = 0; i < ParticleSystems.Count && i < systems.Children.Count; i++)
        {
            var visual = viewportContext.EditingContext.CreateParticleBillboard();
            var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
            visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f * 0.5f);
            var editableObject = new EditableObject(viewportContext.RenderContext, visual, $"{Owner.FullDataPath}_SYSTEM_{i}", -vec3.Ones, vec3.Ones * 2.0f);
            color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
            editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f * 0.25f);
            editableObject.UnselectedColor = visual.Diffuse;
            editableObject.SetPosition(new vec3((i % columns - (columns - 1) / 2.0f) * PreviewSpacing, 0.0f, -(i / columns + 1) * PreviewSpacing));

            var emitter = new ParticleEmitter(viewportContext.RenderContext, $"{Owner.FullDataPath}_SYSTEM_{i}_PARTICLES", pages, i);
            emitter.Configure(ParticleSystems[i], still);
            editableObject.AddChild(emitter);

            var system = systems.Children[i];
            viewportObjects.Add(new ViewportObject(editableObject, system.Path, property)
            {
                Category = ViewportObjectCategory.Particles,
                InspectorFocus = system,
                DuplicatedElement = system,
                RenderDependencies = [system],
                Refresh = () =>
                {
                    emitter.Configure(system.GetValue() as ParticleSystem, still);
                    return true;
                },
            });
        }

        return viewportObjects;
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<LabURI> TextureIDs { get; set; } = new();
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<LabURI> MaterialIDs { get; set; } = new();
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI DecalTextureID { get; set; } = LabURI.Empty;
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI DecalMaterialID { get; set; } = LabURI.Empty;
    
    [JsonProperty(Required = Required.Always)]
    public Byte[] UnkData { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public Byte[] UnkBlob { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public Int32[] UnkInts { get; private set; }
    
    [JsonProperty(Required = Required.Always)]
    public List<Byte[]> UnkBlobs { get; private set; } = new();

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        for (Int32 i = 0; i < 3; i++)
        {
            writer.Write(assetManager.GetAsset(TextureIDs[i]).ExportTwinID);
            writer.Write(assetManager.GetAsset(MaterialIDs[i]).ExportTwinID);
        }

        WriteExport(writer);
        writer.Flush();
        ms.Position -= 4;

        writer.Write(assetManager.GetAsset(DecalTextureID).ExportTwinID);
        writer.Write(assetManager.GetAsset(DecalMaterialID).ExportTwinID);

        writer.Write(UnkData);
        writer.Write(UnkBlob);
        foreach (var @int in UnkInts)
        {
            writer.Write(@int);
        }

        foreach (var blob in UnkBlobs)
        {
            writer.Write(blob);
        }
        writer.Flush();

        ms.Position = 0;
        return factory.GenerateDefaultParticle(ms);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        base.Import(package, variant, layoutId);

        var assetManager = AssetManager.Get();
        ITwinDefaultParticle particle = GetTwinItem<ITwinDefaultParticle>();

        foreach (var texture in particle.TextureIDs)
        {
            TextureIDs.Add(assetManager.GetUriByTwinId<Texture>(Owner, texture));
        }

        foreach (var material in particle.MaterialIDs)
        {
            MaterialIDs.Add(assetManager.GetUriByTwinId<Material>(Owner, material));
        }

        DecalTextureID = assetManager.GetUriByTwinId<Texture>(Owner, particle.DecalTextureID);
        DecalMaterialID = assetManager.GetUriByTwinId<Material>(Owner, particle.DecalMaterialID);

        UnkData = CloneUtils.CloneArray(particle.UnkData);
        UnkBlob = CloneUtils.CloneArray(particle.UnkBlob);
        UnkInts = CloneUtils.CloneArray(particle.UnkInts);

        foreach (var blob in particle.UnkBlobs)
        {
            UnkBlobs.Add(CloneUtils.CloneArray(blob));
        }
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var texturesSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);

        foreach (var texture in TextureIDs)
        {
            assetManager.GetAsset(texture).ResolveChunkResources(factory, texturesSection);
        }

        foreach (var material in MaterialIDs)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(DecalTextureID).ResolveChunkResources(factory, texturesSection);
        assetManager.GetAsset(DecalMaterialID).ResolveChunkResources(factory, materialsSection);

        return base.ResolveChunkResources(factory, section, id);
    }

    protected override void Dispose(Boolean disposing)
    {
        TextureIDs.Clear();
        MaterialIDs.Clear();
        UnkBlobs.Clear();

        base.Dispose(disposing);
    }
}