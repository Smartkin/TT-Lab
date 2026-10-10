using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class SkinData : AbstractAssetData
{
    public const string TlmAssetType = "Skin";
    // The game's biggest skin part has 2716 vertexes before its strips
    private const int BigSkinVertexes = 8000;

    public SkinData(IAsset asset) : base(asset)
    {
        SubSkins = [];
    }

    public SkinData(IAsset asset, ITwinSkin skin) : this(asset)
    {
        SetTwinItem(skin);
    }

    public List<SubSkinData> SubSkins { get; set; }

    public override String GetStringified()
    {
        return Convert.ToHexString(SHA256.HashData(WriteParts(WriteMaterial)));
    }

    // The materials by their IDs: their data hashes are worked out of the file or the loaded data, whichever is there first
    public override Byte[] GetFingerprint()
    {
        return WriteParts((writer, material) => writer.Write(RigidModelData.MaterialId(material)));
    }

    private Byte[] WriteParts(Action<BinaryWriter, LabURI> writeMaterial)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (var subSkin in SubSkins)
        {
            writeMaterial(writer, subSkin.Material);
            ModelData.WritePart(writer, subSkin.Vertexes, subSkin.Faces, subSkin.Layout);
            WriteCompression(writer, subSkin.Compression);
        }

        writer.Flush();
        return stream.ToArray();
    }

    // By what the material holds, its URI changes whenever it's recreated from a glb
    internal static void WriteMaterial(BinaryWriter writer, LabURI material)
    {
        var assetManager = AssetManager.Get();
        writer.Write(material != LabURI.Empty && assetManager.DoesAssetExist(material) ? assetManager.GetAsset(material).GetDataHash() : 0U);
    }

    internal static void WriteCompression(BinaryWriter writer, Twinsanity.TwinsanityInterchange.Common.TwinSkinCompression? compression)
    {
        if (compression == null)
        {
            writer.Write(false);
            return;
        }

        writer.Write(true);
        writer.Write(compression.PositionScale);
        writer.Write(compression.UvScale);
        foreach (var offset in compression.PositionOffset.Concat(compression.UvOffset))
        {
            writer.Write(offset);
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        SubSkins.ForEach(s => s.Dispose());
        SubSkins.Clear();
    }

    public const string TlmKind = "skin";

    /// <summary>
    /// Every subskin as a part of one mesh
    /// </summary>
    public JsonObject WriteTlmMesh(TlmFile file, TlmMaterials materials)
    {
        return TlmMeshes.WriteMesh(file, SubSkins.Select(s => (s.ToModelPart(), materials.Use(s.Material))), true);
    }

    public void ReadTlmMesh(TlmFile file, JsonObject? mesh, TlmMaterials materials)
    {
        foreach (var (part, material) in TlmMeshes.ReadMesh(file, mesh, true))
        {
            if (part.Vertexes.Count > BigSkinVertexes)
            {
                Log.WriteLine($"A skin part of {Owner.Name} has {part.Vertexes.Count} vertexes, the game's biggest skins have around 3000: expect the game to slow down", Log.LogType.Warning);
            }

            SubSkins.Add(new SubSkinData(materials.GetRequired(material, TlmMaterialUse.Skin), part));
        }
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        root[TlmNodes.MeshKey] = WriteTlmMesh(file, new TlmMaterials(file));
        file.Root = root;
        file.Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        var materials = new TlmMaterials(file, Owner);
        SubSkins = [];
        ReadTlmMesh(file, file.Root?[TlmNodes.MeshKey] as JsonObject, materials);
        DisposedValue = false;
        if (materials.AddedToProject)
        {
            SaveInternal(dataPath, settings);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var skin = GetTwinItem<ITwinSkin>();
        SubSkins = [];
        foreach (var subSkin in skin.SubSkins)
        {
            SubSkins.Add(new SubSkinData(Owner, subSkin));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        TlmMaterials.CheckDrawsSkins(Owner, SubSkins.Select(s => s.Material));
        var assetManager = AssetManager.Get();
        return factory.GenerateSkin(SubSkins.Select(s => new SkinPartExport(assetManager.GetAsset(s.Material).ExportTwinID, s.Vertexes,
            StripParts.GetValidLayout(s.Layout, s.Vertexes, s.Faces, StripParts.SkinWinding), s.Compression)).ToList());
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        foreach (var subSkin in SubSkins)
        {
            assetManager.GetAsset(subSkin.Material).ResolveChunkResources(factory, materialsSection);
        }
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}
