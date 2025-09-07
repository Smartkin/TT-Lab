using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Instance.Scenery;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance.Scenery
{
    [ReferencesAssets]
    public class SceneryBaseData
    {
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> MeshIDs { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> LodIDs { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Vector4[]> BoundingBoxes { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Matrix4> MeshModelMatrices { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Matrix4> LodModelMatrices { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4 UnkVec1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4 UnkVec2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4 UnkVec3 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4 UnkVec4 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Boolean[] LightsEnabler { get; set; }

        public SceneryBaseData() { }

        public SceneryBaseData(IAsset owner, TwinSceneryBaseType baseType)
        {
            MeshIDs = new List<LabURI>();
            foreach (var m in baseType.MeshIDs)
            {
                MeshIDs.Add(AssetManager.Get().GetUriByTwinId<Mesh>(owner, m));
            }
            LodIDs = new List<LabURI>();
            foreach (var l in baseType.LodIDs)
            {
                LodIDs.Add(AssetManager.Get().GetUriByTwinId<LodModel>(owner, l));
            }
            BoundingBoxes = new List<Vector4[]>();
            foreach (var bb in baseType.BoundingBoxes)
            {
                BoundingBoxes.Add([CloneUtils.Clone(bb[0]), CloneUtils.Clone(bb[1])]);
            }
            MeshModelMatrices = new List<Matrix4>();
            foreach (var mat in baseType.MeshModelMatrices)
            {
                MeshModelMatrices.Add(CloneUtils.DeepClone(mat));
            }
            LodModelMatrices = new List<Matrix4>();
            foreach (var mat in baseType.LodModelMatrices)
            {
                LodModelMatrices.Add(CloneUtils.DeepClone(mat));
            }
            UnkVec1 = CloneUtils.Clone(baseType.UnkVec1);
            UnkVec2 = CloneUtils.Clone(baseType.UnkVec2);
            UnkVec3 = CloneUtils.Clone(baseType.UnkVec3);
            UnkVec4 = CloneUtils.Clone(baseType.UnkVec4);
            LightsEnabler = CloneUtils.CloneArray(baseType.LightsEnabler);
        }

        public SceneryBaseData(BaseSceneryViewModel vm)
        {
            MeshIDs = CloneUtils.CloneList(vm.Meshes.ToList().Select((e) => e.Value).ToList(), (e) => new(e.GetUri()));
            LodIDs = CloneUtils.CloneList(vm.Lods.ToList().Select((e) => e.Value).ToList(), (e) => new(e.GetUri()));
            BoundingBoxes = new List<Vector4[]>();
            foreach (var bb in vm.Bbs)
            {
                BoundingBoxes.Add(new Vector4[]
                {
                    new Vector4
                    {
                        X = bb.TopLeft.X,
                        Y = bb.TopLeft.Y,
                        Z = bb.TopLeft.Z,
                        W = bb.TopLeft.W,
                    },
                    new Vector4
                    {
                        X = bb.BottomRight.X,
                        Y = bb.BottomRight.Y,
                        Z = bb.BottomRight.Z,
                        W = bb.BottomRight.W,
                    }
                });
            }
            MeshModelMatrices = new List<Matrix4>();
            foreach (var mat in vm.MeshModelMatrices)
            {
                var V1 = new Vector4
                {
                    X = mat[0].X,
                    Y = mat[0].Y,
                    Z = mat[0].Z,
                    W = mat[0].W,
                };
                var V2 = new Vector4
                {
                    X = mat[1].X,
                    Y = mat[1].Y,
                    Z = mat[1].Z,
                    W = mat[1].W,
                };
                var V3 = new Vector4
                {
                    X = mat[2].X,
                    Y = mat[2].Y,
                    Z = mat[2].Z,
                    W = mat[2].W,
                };
                var V4 = new Vector4
                {
                    X = mat[3].X,
                    Y = mat[3].Y,
                    Z = mat[3].Z,
                    W = mat[3].W,
                };
                MeshModelMatrices.Add(new Matrix4
                {
                    Column1 = V1,
                    Column2 = V2,
                    Column3 = V3,
                    Column4 = V4,
                });
            }
            LodModelMatrices = new List<Matrix4>();
            foreach (var mat in vm.LodModelMatrices)
            {
                var V1 = new Vector4
                {
                    X = mat[0].X,
                    Y = mat[0].Y,
                    Z = mat[0].Z,
                    W = mat[0].W,
                };
                var V2 = new Vector4
                {
                    X = mat[1].X,
                    Y = mat[1].Y,
                    Z = mat[1].Z,
                    W = mat[1].W,
                };
                var V3 = new Vector4
                {
                    X = mat[2].X,
                    Y = mat[2].Y,
                    Z = mat[2].Z,
                    W = mat[2].W,
                };
                var V4 = new Vector4
                {
                    X = mat[3].X,
                    Y = mat[3].Y,
                    Z = mat[3].Z,
                    W = mat[3].W,
                };
                LodModelMatrices.Add(new Matrix4
                {
                    Column1 = V1,
                    Column2 = V2,
                    Column3 = V3,
                    Column4 = V4,
                });
            }
            UnkVec1 = new Vector4
            {
                X = vm.UnkVec1.X,
                Y = vm.UnkVec1.Y,
                Z = vm.UnkVec1.Z,
                W = vm.UnkVec1.W,
            };
            UnkVec2 = new Vector4
            {
                X = vm.UnkVec2.X,
                Y = vm.UnkVec2.Y,
                Z = vm.UnkVec2.Z,
                W = vm.UnkVec2.W,
            };
            UnkVec3 = new Vector4
            {
                X = vm.UnkVec3.X,
                Y = vm.UnkVec3.Y,
                Z = vm.UnkVec3.Z,
                W = vm.UnkVec3.W,
            };
            UnkVec4 = new Vector4
            {
                X = vm.UnkVec4.X,
                Y = vm.UnkVec4.Y,
                Z = vm.UnkVec4.Z,
                W = vm.UnkVec4.W,
            };
            LightsEnabler = new Boolean[128];
            var index = 0;
            foreach (var enabler in vm.LightsEnabler)
            {
                LightsEnabler[index++] = enabler.Value;
            }
        }

        public virtual ITwinScenery.SceneryType GetSceneryType()
        {
            return ITwinScenery.SceneryType.Leaf;
        }

        public virtual void Write(BinaryWriter writer)
        {
            var assetManager = AssetManager.Get();
            var hasMeshLods = MeshIDs.Count > 0 || LodIDs.Count > 0 ? 0x1613 : 0;
            writer.Write(hasMeshLods);
            if (hasMeshLods != 0)
            {
                writer.Write((Int16)MeshIDs.Count);
                writer.Write((Int16)LodIDs.Count);

                foreach (var bb in BoundingBoxes)
                {
                    bb[0].Write(writer);
                    bb[1].Write(writer);
                }

                foreach (var mesh in MeshIDs)
                {
                    writer.Write(assetManager.GetAsset(mesh).ID);
                }

                foreach (var lod in LodIDs)
                {
                    writer.Write(assetManager.GetAsset(lod).ID);
                }

                foreach (var mat in MeshModelMatrices)
                {
                    mat.Write(writer);
                }

                foreach (var mat in LodModelMatrices)
                {
                    mat.Write(writer);
                }
            }

            UnkVec1.Write(writer);
            UnkVec2.Write(writer);
            UnkVec3.Write(writer);
            UnkVec4.Write(writer);
            var bytes = new Byte[16];
            for (var i = 0; i < 16; ++i)
            {
                for (var j = 0; j < 8; ++j)
                {
                    var num = LightsEnabler[j + i * 8] ? 1 : 0;
                    bytes[i] |= (Byte)(num << j);
                }
            }
            writer.Write(bytes);
        }

        public void ResolveChunkResouces(ITwinItemFactory factory, ITwinSection section)
        {
            var assetManager = AssetManager.Get();
            var meshSection = section.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
            var lodSection = section.GetItem<ITwinSection>(Constants.GRAPHICS_LODS_SECTION);

            foreach (var mesh in MeshIDs)
            {
                assetManager.GetAsset(mesh).ResolveChunkResources(factory, meshSection);
            }

            foreach (var lod in LodIDs)
            {
                assetManager.GetAsset(lod).ResolveChunkResources(factory, lodSection);
            }
        }
    }
}
