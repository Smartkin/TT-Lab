using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;

namespace TT_Lab.AssetData.Graphics;

/// <summary>
/// A model and the materials its submodels are drawn with
/// </summary>
[ReferencesAssets]
public class RigidModelData : AbstractAssetData
{
    public const string TlmAssetType = "RigidModel";

    public RigidModelData(IAsset asset) : base(asset)
    {
        Materials = new List<LabURI>();
        Model = LabURI.Empty;
    }

    public RigidModelData(IAsset asset, ITwinRigidModel rigidModel) : this(asset)
    {
        SetTwinItem(rigidModel);
    }

    public List<LabURI> Materials { get; set; }
    public LabURI Model { get; set; }

    protected virtual string AssetTlmType => TlmAssetType;
    protected virtual Int32 ExportHeader => 257;

    protected override void Dispose(Boolean disposing)
    {
        Materials.Clear();
    }

    public override String GetStringified()
    {
        var assetManager = AssetManager.Get();
        var result = new StringBuilder();
        result.AppendLine(assetManager.GetAsset(Model).GetDataHash().ToString());
        foreach (var mat in Materials)
        {
            result.AppendLine(assetManager.GetAsset(mat).GetDataHash().ToString());
        }

        return result.ToString();
    }

    protected virtual string TlmKind => "rigid_model";

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(AssetTlmType, Owner.Name);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        root[TlmNodes.MeshKey] = WriteTlmMesh(file, new TlmMaterials(file));
        file.Root = root;
        file.Save(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        var materials = new TlmMaterials(file, Owner);
        var parts = TlmMeshes.ReadMesh(file, file.Root?[TlmNodes.MeshKey] as JsonObject, false, file.Root?.GetTransform());
        SetFromParts(Owner, parts.Select(p => (p.Part, materials.Get(p.Material))).ToList(), Owner.Name);
        DisposedValue = false;
        if (materials.AddedToProject)
        {
            SaveInternal(dataPath, settings);
        }
    }

    /// <summary>
    /// The model's submodels as the parts of one mesh with the materials they're drawn with
    /// </summary>
    public JsonObject WriteTlmMesh(TlmFile file, TlmMaterials materials)
    {
        var modelData = AssetManager.Get().GetAssetData<ModelData>(Model);
        return TlmMeshes.WriteMesh(file, modelData.GetParts().Select((part, i) => (part, materials.Use(i < Materials.Count ? Materials[i] : LabURI.Empty))), false);
    }

    /// <summary>
    /// Creates an internal rigid model or mesh out of the parts of a mesh
    /// </summary>
    /// <param name="owner">Asset whose data the new asset is part of</param>
    /// <param name="file">File the mesh is in</param>
    /// <param name="mesh">The mesh</param>
    /// <param name="materials">Materials of the file</param>
    /// <param name="transform">Where the mesh's node was moved to, baked into the vertexes</param>
    /// <param name="name">Name of the new asset</param>
    public static T ReadTlm<T>(IAsset owner, TlmFile file, JsonObject mesh, TlmMaterials materials, System.Numerics.Matrix4x4? transform, string name) where T : RigidModel, new()
    {
        var asset = new T
        {
            Package = owner.Package,
            InvariantName = $"{owner.Name}_{SanitizeName(name)}",
            Alias = name,
            IsInternal = true,
            InternalOwner = owner
        };
        RigidModelData data = typeof(T) == typeof(Mesh) ? new MeshData(asset) : new RigidModelData(asset);
        data.SetFromParts(owner, TlmMeshes.ReadMesh(file, mesh, false, transform).Select(p => (p.Part, materials.Get(p.Material))).ToList(), name);
        asset.SetData(data);
        AssetManager.Get().TryAddAsset(asset);
        return asset;
    }

    public static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) || c == '/' ? '_' : c).ToArray());
    }

    public void SetFromParts(IAsset owner, List<(ModelPart Part, LabURI Material)> parts, string name)
    {
        var model = new Model
        {
            Package = owner.Package,
            InvariantName = $"{owner.Name}_{SanitizeName(name)}_Model",
            Alias = $"{name} Model",
            IsInternal = true,
            InternalOwner = owner
        };
        var modelData = new ModelData(model);
        modelData.SetParts(parts.Select(p => p.Part));
        model.SetData(modelData);
        AssetManager.Get().TryAddAsset(model);

        Model = model.URI;
        Materials = parts.Select(p => p.Material).ToList();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinRigidModel rigidModel = GetTwinItem<ITwinRigidModel>();
        Materials = new List<LabURI>();
        foreach (var mat in rigidModel.Materials)
        {
            Materials.Add(AssetManager.Get().GetUriByTwinId<Material>(Owner, mat));
        }
        Model = AssetManager.Get().GetUriByTwinId<Model>(Owner, rigidModel.Model);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(ExportHeader);
        writer.Write(Materials.Count);
        foreach (var mat in Materials)
        {
            writer.Write(assetManager.GetAsset(mat).ExportTwinID);
        }
        writer.Write(assetManager.GetAsset(Model).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return CreateItem(factory, ms);
    }

    protected virtual ITwinItem CreateItem(ITwinItemFactory factory, Stream stream)
    {
        return factory.GenerateRigidModel(stream);
    }

    protected virtual void ResolveResources(ITwinItemFactory factory, ITwinSection section)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var modelsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);

        foreach (var material in Materials)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(Model).ResolveChunkResources(factory, modelsSection);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        ResolveResources(factory, section);
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}
