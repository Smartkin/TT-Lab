using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.DynamicScenery
{
    public class TwinDynamicSceneryModel : ITwinSerializable
    {
        public Int32 UnkInt { get; set; }
        public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
        public Int32 AnimatedFrames { get; set; }
        public TwinDynamicSceneryAnimation Animation { get; set; }
        public Byte LodFlag { get; set; }
        public UInt32 MeshID { get; set; }
        public Vector4[] BoundingBox { get; set; }

        public TwinDynamicSceneryModel()
        {
            BoundingBoxBuilders = new List<TwinBoundingBoxBuilder>();
            BoundingBox = new Vector4[2];
            Animation = new TwinDynamicSceneryAnimation();
        }

        public Int32 GetLength()
        {
            return 4 + 4 + BoundingBoxBuilders.Sum(o => o.GetLength()) + 4 + Animation.GetLength() + 1 + 4 + 2 * Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            UnkInt = reader.ReadInt32();
            var bbBuildersAmount = reader.ReadInt32();
            if (bbBuildersAmount != 0)
            {
                for (var i = 0; i < bbBuildersAmount; ++i)
                {
                    var bbBuilder = new TwinBoundingBoxBuilder();
                    BoundingBoxBuilders.Add(bbBuilder);
                    bbBuilder.Read(reader, length);
                }
            }
            AnimatedFrames = reader.ReadInt32();
            Animation.Read(reader, length);
            LodFlag = reader.ReadByte();
            MeshID = reader.ReadUInt32();
            for (var i = 0; i < 2; ++i)
            {
                BoundingBox[i] = new Vector4();
                BoundingBox[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(UnkInt);
            writer.Write(BoundingBoxBuilders.Count);
            foreach (var bbBuilder in BoundingBoxBuilders)
            {
                bbBuilder.Write(writer);
            }
            writer.Write(AnimatedFrames);
            Animation.Write(writer);
            writer.Write(LodFlag);
            writer.Write(MeshID);
            foreach (var v in BoundingBox)
            {
                v.Write(writer);
            }
        }
    }
}
