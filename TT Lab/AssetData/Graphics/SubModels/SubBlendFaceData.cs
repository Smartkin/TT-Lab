using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class SubBlendFaceData : IDisposable
    {
        public UInt32 VertexesAmount { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<BlendShapeData> BlendShapes { get; set; } = new();

        public SubBlendFaceData() { }

        public SubBlendFaceData(ITwinBlendSkinFace face, List<int> usedIdxs)
        {
            VertexesAmount = face.VertexesAmount;

            face.CalculateData();
            var idx = 0;
            foreach (var shape in face.Vertices)
            {
                if (!usedIdxs.Contains(idx++))
                {
                    continue;
                }
                
                var shapeData = new BlendShapeData()
                {
                    Offset = CloneUtils.Clone(shape.Offset)
                };
                BlendShapes.Add(shapeData);
            }
        }

        public SubBlendFaceData(IEnumerable<System.Numerics.Vector3> positions)
        {
            VertexesAmount = (UInt32)positions.Count();

            foreach (var pos in positions)
            {
                var shapeData = new BlendShapeData()
                {
                    Offset = pos.ToTwin()
                };
                BlendShapes.Add(shapeData);
            }
        }

        public void Dispose()
        {
            BlendShapes.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
