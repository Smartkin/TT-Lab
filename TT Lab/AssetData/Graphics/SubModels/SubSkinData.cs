using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels;

[ReferencesAssets]
public class SubSkinData : IDisposable
{
    public LabURI Material { get; set; }
    public List<Vertex> Vertexes { get; set; }
    public List<IndexedFace> Faces { get; set; }
    public MeshProcessor.Mesh Mesh { get; set; }

    public SubSkinData(IAsset owner, ITwinSubSkin subSkin)
    {
        Material = AssetManager.Get().GetUriByTwinId<Assets.Graphics.Material>(owner, subSkin.Material);
        if (Material == LabURI.Empty)
        {
            var allMaterials = AssetManager.Get().GetAssets().FindAll(a => a is Material).ConvertAll(a => a.URI);
            var actuallyHasTheMaterial = allMaterials.FindAll(uri => uri.ToString().Contains(subSkin.Material.ToString()));
            throw new Exception($"Couldn't find requested material 0x{subSkin.Material:X}!");
        }

        Vertexes = new List<Vertex>();
        Faces = new List<IndexedFace>();

        subSkin.CalculateData();
        var winding = false;
        var e = subSkin;
        var tempFaceList = new List<IndexedFace>();
        for (var j = 2; j < e.Vertexes.Count; ++j)
        {
            if (!e.SkinJoints[j].Connection)
            {
                winding = !winding;
                continue;
            }

            int[] triIndices;
            if (!winding)
            {
                triIndices = [j - 2, j - 1, j];
            }
            else
            {
                triIndices = [j - 1, j - 2, j];
            }
            
            tempFaceList.Add(new IndexedFace(triIndices));
            winding = !winding;
        }
        
        foreach (var face in tempFaceList)
        {
            var verIdx = 0;
            var newFace = new IndexedFace(0, 0, 0);
            foreach (var j in face.Indexes!)
            {
                var ver = GetVertexFromSubSkin(subSkin, j);

                if (!Vertexes.Contains(ver))
                {
                    Vertexes.Add(ver);
                }
                newFace.Indexes![verIdx++] = Vertexes.IndexOf(ver);
            }
            Faces.Add(newFace);
        }

        Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces);
        MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
    }

    private static Vertex GetVertexFromSubSkin(ITwinSubSkin subSkin, int i)
    {
        return new Vertex(subSkin.Vertexes[i], subSkin.Colors[i], subSkin.UVW[i], subSkin.Colors[i])
        {
            JointInfo = CloneUtils.Clone(subSkin.SkinJoints[i])
        };
    }

    public SubSkinData(LabURI material, List<Vertex> vertexes, List<IndexedFace> faces)
    {
        Material = material;
        Vertexes = vertexes;
        Faces = faces;
        Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces);
        MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
    }

    public void Dispose()
    {
        Vertexes.Clear();
        Faces.Clear();
        Mesh.Clear();

        GC.SuppressFinalize(this);
    }
}