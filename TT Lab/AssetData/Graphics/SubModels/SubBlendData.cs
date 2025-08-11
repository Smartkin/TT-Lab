using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels
{
    [ReferencesAssets]
    public class SubBlendData : IDisposable
    {
        public LabURI Material { get; set; }
        public List<SubBlendModelData> Models { get; set; } = [];

        public SubBlendData(LabURI package, String? variant, ITwinSubBlendSkin blend)
        {
            Material = AssetManager.Get().GetUri(package, nameof(Assets.Graphics.Material), variant, blend.Material);
            if (Material == LabURI.Empty)
            {
                var allMaterials = AssetManager.Get().GetAssets().FindAll(a => a is Assets.Graphics.Material).ConvertAll(a => a.URI);
                var actuallyHasTheMaterial = allMaterials.FindAll(uri => uri.ToString().Contains(blend.Material.ToString()));
                throw new Exception($"Couldn't find requested material 0x{blend.Material:X}!");
            }

            foreach (var model in blend.Models)
            {
                Models.Add(new SubBlendModelData(model));
            }
        }

        public SubBlendData(LabURI material, IEnumerable<SharpGLTF.Schema2.Mesh> meshes, Int32 blendsAmount)
        {
            Material = material;

            foreach (var mesh in meshes)
            {
                var allVertexes = new List<Vertex>();
                var allIndices = new List<IndexedFace>();
                var indiceAccessor = 0;
                var blendShapeJson = mesh.Extras;
                var blendShape = new Vector3((float)blendShapeJson["X"]!, (float)blendShapeJson["Y"]!, (float)blendShapeJson["Z"]!);
                var primitive = mesh.Primitives[0];
                var vertexes = primitive.GetVertexColumns();
                var indices = primitive.GetTriangleIndices();
                var amountOfSubmodels = (indices.Count() * 3) / TwinVIFCompiler.VertexStripCache;
                var leftovers = (indices.Count() * 3) % TwinVIFCompiler.VertexStripCache;
                if (leftovers > 0)
                {
                    amountOfSubmodels++;
                }

                for (var i = 0; i < vertexes.Positions.Count; i++)
                {
                    var pos = vertexes.Positions[i].ToTwin();
                    pos.W = vertexes.Colors1[i].X;
                    var ver = new Vertex(
                        pos,
                        vertexes.Colors0[i].ToTwin(),
                        vertexes.TexCoords0[i].ToTwin());
                    ver.UV.Z = vertexes.TexCoords1[i].X;
                    ver.UV.W = vertexes.TexCoords1[i].Y;
                    ver.Color = new Vector4(ver.Color.X, ver.Color.Y, ver.Color.Z, ver.Color.W);
                    ver.JointInfo.JointIndex1 = (Int32)vertexes.Joints0[i].X;
                    ver.JointInfo.JointIndex2 = (Int32)vertexes.Joints0[i].Y;
                    ver.JointInfo.JointIndex3 = (Int32)vertexes.Joints0[i].Z;
                    ver.JointInfo.Weight1 = vertexes.Weights0[i].X;
                    ver.JointInfo.Weight2 = vertexes.Weights0[i].Y;
                    ver.JointInfo.Weight3 = vertexes.Weights0[i].Z;

                    allVertexes.Add(ver);
                }

                foreach (var (idx1, idx2, idx3) in indices)
                {
                    allIndices.Add(new IndexedFace(idx1, idx2, idx3));
                }

                if (vertexes.MorphTargets.Count == 0)
                {
                    var zeros = new List<List<System.Numerics.Vector3>>();
                    for (var j = 0; j < blendsAmount; j++)
                    {
                        zeros.Add([]);
                        for (var i = 0; i < vertexes.Positions.Count; i++)
                        {
                            zeros[^1].Add(System.Numerics.Vector3.Zero);
                        }
                    }
                    Models.Add(new SubBlendModelData(blendShape, allVertexes, allIndices, zeros, true));
                    continue;
                }

                Models.Add(new SubBlendModelData(blendShape, allVertexes, allIndices, vertexes.MorphTargets));
            }
        }

        public void Dispose()
        {
            foreach (var model in Models)
            {
                model.Dispose();
            }
            Models.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
