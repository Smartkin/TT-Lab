using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels;

public class SubBlendModelData : IDisposable
{
    public Vector3 BlendShape { get; set; }
    public List<SubBlendFaceData> BlendFaces { get; set; } = [];
    public List<IndexedFace> Faces { get; set; } = [];
    public List<Vertex> Vertexes { get; set; } = [];
    public bool FacesSquashedOnExport { get; } = false;
    public MeshProcessor.Mesh Mesh { get; set; }

    public SubBlendModelData(ITwinBlendSkinModel model)
    {
        model.CalculateData();
        
        var winding = false;
        var e = model;
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

        var usedIdxList = new List<int>();
        foreach (var face in tempFaceList)
        {
            var verIdx = 0;
            var newFace = new IndexedFace(0, 0, 0);
            foreach (var j in face.Indexes!)
            {
                var ver = GetVertexFromBlendSubSkin(model, j);

                if (!Vertexes.Contains(ver))
                {
                    Vertexes.Add(ver);
                    usedIdxList.Add(j);
                }
                newFace.Indexes![verIdx++] = Vertexes.IndexOf(ver);
            }
            Faces.Add(newFace);
        }

        BlendShape = CloneUtils.Clone(model.BlendShape);
        foreach (var blendFace in model.Faces)
        {
            BlendFaces.Add(new SubBlendFaceData(blendFace, usedIdxList));
        }

        Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces, BlendFaces);
        MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
    }

    private static Vertex GetVertexFromBlendSubSkin(ITwinBlendSkinModel model, int i)
    {
        return new Vertex(model.Vertexes[i], model.Colors[i], model.UVW[i], model.Colors[i])
        {
            JointInfo = CloneUtils.Clone(model.SkinJoints[i])
        };
    }

    public SubBlendModelData(Vector3 blendShape, List<Vertex> vertexes, List<IndexedFace> faces, List<List<System.Numerics.Vector3>> morphTargets, bool facesSquashedOnExport = false)
    {
        BlendShape = blendShape;
        Vertexes = vertexes;
        Faces = faces;
        FacesSquashedOnExport = facesSquashedOnExport;

        foreach (var morph in morphTargets)
        {
            Debug.Assert(Vertexes.Count == morph.Count, "Morph must have the same amount of vertexes as the model!");
            BlendFaces.Add(new SubBlendFaceData(morph));
        }

        Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces, BlendFaces);
        MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
    }

    public SubBlendModelData(Vector3 blendShape, List<Vertex> vertexes, List<IndexedFace> faces, IEnumerable<SharpGLTF.Geometry.VertexBufferColumns> morphTargets)
    {
        BlendShape = blendShape;
        Vertexes = vertexes;
        Faces = faces;

        foreach (var morph in morphTargets)
        {
            Debug.Assert(Vertexes.Count == morph.Positions.Count, "Morph must have the same amount of vertexes as the model!");
            BlendFaces.Add(new SubBlendFaceData(morph.Positions));
        }

        Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces, BlendFaces);
        MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
    }

    public void Dispose()
    {
        foreach (var face in BlendFaces)
        {
            face.Dispose();
        }
        BlendFaces.Clear();
        Faces.Clear();
        Vertexes.Clear();

        GC.SuppressFinalize(this);
    }
}