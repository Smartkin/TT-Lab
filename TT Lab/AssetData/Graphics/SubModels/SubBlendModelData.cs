using System;
using System.Collections.Generic;
using System.Diagnostics;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels
{
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

            BlendShape = CloneUtils.Clone(model.BlendShape);
            foreach (var blendFace in model.Faces)
            {
                BlendFaces.Add(new SubBlendFaceData(blendFace));
            }

            Int32 refIndex = 0;
            for (var i = 0; i < model.Vertexes.Count; ++i)
            {
                if (i < model.Vertexes.Count - 2)
                {
                    if (model.SkinJoints[i + 2].Connection)
                    {
                        if (i % 2 == 0)
                        {
                            Faces.Add(new IndexedFace(new int[] { refIndex, refIndex + 1, refIndex + 2 }));
                        }
                        else
                        {
                            Faces.Add(new IndexedFace(new int[] { refIndex + 1, refIndex, refIndex + 2 }));
                        }
                    }
                    ++refIndex;
                }
                Vertexes.Add(new Vertex(model.Vertexes[i], model.Colors[i], model.UVW[i], model.Colors[i])
                {
                    JointInfo = CloneUtils.Clone(model.SkinJoints[i])
                });
            }

            Mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes, Faces, BlendFaces);
            MeshProcessor.MeshProcessor.ProcessMesh(Mesh);
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
}
