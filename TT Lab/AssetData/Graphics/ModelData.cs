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
using TT_Lab.MeshProcessor;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

/// <summary>
/// Geometry of a rigid model, a list of submodels each drawn with its own material
/// </summary>
public class ModelData : AbstractAssetData
{
    public const string TlmAssetType = "Model";

    public ModelData(IAsset asset) : base(asset)
    {
    }

    public ModelData(IAsset asset, ITwinModel model) : this(asset)
    {
        SetTwinItem(model);
    }

    /// <summary>
    /// Distinct vertexes of every submodel
    /// </summary>
    public List<List<Vertex>> Vertexes { get; set; } = [];
    /// <summary>
    /// Triangles of every submodel facing the way their normals point
    /// </summary>
    public List<List<IndexedFace>> Faces { get; set; } = [];
    /// <summary>
    /// The strips every submodel was packed into, null when they have to be built anew
    /// </summary>
    public List<StripLayout?> Layouts { get; set; } = [];

    protected override void Dispose(Boolean disposing)
    {
        Vertexes.Clear();
        Faces.Clear();
        Layouts.Clear();
    }

    public override String GetStringified()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        for (var i = 0; i < Vertexes.Count; i++)
        {
            WritePart(writer, Vertexes[i], Faces[i], Layouts.ElementAtOrDefault(i));
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    internal static void WritePart(BinaryWriter writer, List<Vertex> vertexes, List<IndexedFace> faces, StripLayout? layout)
    {
        writer.Write(vertexes.Count);
        foreach (var vertex in vertexes)
        {
            vertex.WriteBinary(writer);
        }

        writer.Write(faces.Count);
        foreach (var face in faces)
        {
            writer.Write(face.Indexes![0]);
            writer.Write(face.Indexes[1]);
            writer.Write(face.Indexes[2]);
        }

        if (layout == null)
        {
            writer.Write(-1);
            return;
        }

        var (strips, batchSizes) = layout.ToArrays();
        writer.Write((Int32)layout.Padding);
        foreach (var value in strips.Concat(batchSizes))
        {
            writer.Write(value);
        }

        // Only the Xbox version's layouts have these
        if (layout.IgnoresFacing)
        {
            writer.Write(true);
            foreach (var joints in layout.Batches.Select(b => b.Joints ?? []))
            {
                writer.Write(joints.Count);
                joints.ForEach(writer.Write);
            }
        }
    }

    public List<ModelPart> GetParts()
    {
        return Vertexes.Select((vertexes, i) => new ModelPart
        {
            Vertexes = vertexes,
            Faces = Faces[i],
            Layout = Layouts.ElementAtOrDefault(i)
        }).ToList();
    }

    public void SetParts(IEnumerable<ModelPart> parts)
    {
        Vertexes.Clear();
        Faces.Clear();
        Layouts.Clear();
        foreach (var part in parts)
        {
            Vertexes.Add(part.Vertexes);
            Faces.Add(part.Faces);
            Layouts.Add(part.Layout);
        }
    }

    public const string TlmKind = "model";

    /// <summary>
    /// Every submodel as a part of one mesh, models have no materials of their own
    /// </summary>
    public JsonObject WriteTlmMesh(TlmFile file)
    {
        return TlmMeshes.WriteMesh(file, GetParts().Select(part => (part, -1)), false);
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        root[TlmNodes.MeshKey] = WriteTlmMesh(file);
        file.Root = root;
        file.Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        SetParts(TlmMeshes.ReadMesh(file, file.Root?[TlmNodes.MeshKey] as JsonObject, false, file.Root?.GetTransform()).Select(p => p.Part));
        DisposedValue = false;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var model = GetTwinItem<ITwinModel>();
        Vertexes = [];
        Faces = [];
        Layouts = [];
        foreach (var subModel in model.SubModels)
        {
            var part = StripParts.FromRigid(subModel);
            Vertexes.Add(part.Vertexes);
            Faces.Add(part.Faces);
            Layouts.Add(part.Layout);
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var parts = new List<RigidPartExport>();
        for (var i = 0; i < Vertexes.Count; ++i)
        {
            parts.Add(new RigidPartExport(Vertexes[i], StripParts.GetValidLayout(Layouts.ElementAtOrDefault(i), Vertexes[i], Faces[i], StripParts.RigidWinding)));
        }

        return factory.GenerateModel(parts);
    }
}
