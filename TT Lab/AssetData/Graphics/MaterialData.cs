using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using GlmSharp;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class MaterialData : AbstractAssetData
{
    // The game's material has 4 slots for its shaders' pointers with their count right after them, a 5th overwrote the count
    public const int MaxShaders = 4;

    public MaterialData(IAsset asset) : base(asset)
    {
        Shaders = [new LabShader()];
        Name = "NewMaterial";
    }

    public MaterialData(IAsset asset, ITwinMaterial material) : this(asset)
    {
        SetTwinItem(material);
    }

    public static MaterialData GetEmptyMaterial()
    {
        var material = new MaterialData(null);
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        material.Shaders[0].ShaderType = TwinShader.Type.StandardLit;
        material.Shaders[0].TextureId = LabURI.BoatGuy;
        return material;
    }

    /// <summary>
    /// A bit for each type of shader the material has. The game keeps it as the key of the VU1 programs its render bucket has loaded
    /// (<c>RenderMaterial</c>): a material with the key of the last one drawn in the bucket doesn't load its shaders' programs, and 1
    /// never does. Building writes what the shaders need (<see cref="DeriveActivatedShaders"/>), which is what every retail material has
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The types of the material's shaders: the game keeps a bit for each and loads the shaders' programs by them. Building writes them from the shaders",
              EditorDescType = typeof(ActivatedShadersEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    [EditorReadOnly]
    public AppliedShaders ActivatedShaders { get; set; }

    /// <summary>
    /// The shader types' bits, or the stored ones when a shader has a type no retail material has, whose bit isn't known
    /// </summary>
    public AppliedShaders DeriveActivatedShaders()
    {
        AppliedShaders result = 0;
        foreach (var shader in Shaders)
        {
            if (!Enum.TryParse<AppliedShaders>(shader.ShaderType.ToString(), out var bit))
            {
                return ActivatedShaders;
            }

            result |= bit;
        }

        return result;
    }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -2)]
    [EditorParam(TextFieldViewModel.TextFieldAsciiOnly, true)]
    public String Name { get; set; }
    
    /// <summary>
    /// Which of the game's 28 render buckets (DMA chain managers) the material's draws go into, drawn in bucket order, see
    /// <see cref="RenderBuckets"/>. Buckets only order the draws, the look is the shaders' in any of them
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Render bucket", Hint = "Which of the game's 28 render buckets the material's draws go into, drawn in bucket order: 0 skydomes (right after the screen's clear), 2 opaque scenery and objects, 3 opaque global objects, 6-19 alpha-blended (later ones draw over earlier ones), 22 particles, 24 the UI, 26 fonts. A bucket doesn't change how the material looks, only when it's drawn", EditorDescType = typeof(RenderBucketEditorDesc))]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -1)]
    public UInt32 DmaChainIndex { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Shaders", Hint = "The game keeps a material's shaders in 4 slots, the count comes right after them (ReadMaterialShader)")]
    [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Shader")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxShaders)]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, 1)]
    public List<LabShader> Shaders { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Shaders.Clear();
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var context = viewportContext.RenderContext;
        var preview = new MaterialPreview(context, this);
        var editableObject = new EditableObject(context, preview, "MATERIAL_PREVIEW_EDITABLE", new vec3(-1, 0, -1), new vec3(2, 2, 2))
        {
            IsSelectable = false
        };
        var data = property.Find(nameof(SerializableAsset.AssetData)) ?? property;
        return [new ViewportObject(editableObject, $"MATERIAL_PREVIEW_{property.Path}", property)
        {
            RenderDependencies = [data],
            Refresh = () =>
            {
                preview.Refresh();
                return true;
            }
        }];
    }

    public override String GetStringified()
    {
        var result = new StringBuilder();
        result.AppendLine(ActivatedShaders.ToString());
        result.AppendLine(DmaChainIndex.ToString());
        foreach (var shader in Shaders)
        {
            result.AppendLine(shader.GetStringified());
        }
        
        return result.ToString();
    }
    
    

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var material = GetTwinItem<ITwinMaterial>();
        ActivatedShaders = material.ActivatedShaders;
        DmaChainIndex = material.DmaChainIndex;
        Name = new string(material.Name.ToCharArray());
        Shaders = [];
        foreach (var shader in material.Shaders)
        {
            Shaders.Add(new LabShader(Owner, shader));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("shaders", Shaders.Count, MaxShaders);
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((UInt64)DeriveActivatedShaders());
        writer.Write(DmaChainIndex);
        writer.Write(Name.Length + 1);
        GameText.Write(writer, Name + '\0');
        writer.Write(Shaders.Count);
        foreach (var shader in Shaders)
        {
            shader.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateMaterial(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var texturesSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_TEXTURES_SECTION);
        foreach (var shader in Shaders.Where(shader => shader.TextureId != LabURI.Empty))
        {
            assetManager.GetAsset(shader.TextureId).ResolveChunkResources(factory, texturesSection);
        }
        
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}