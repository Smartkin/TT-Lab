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
        /// <summary>
        /// The first word of every retail dynamic model
        /// </summary>
        public const Int32 GameLeftover = 8;
        /// <summary>
        /// The tools' first word of the model (<see cref="GameLeftover"/> everywhere), the game sets it to -1 before reading it and
        /// never reads it again
        /// </summary>
        public Int32 LeftoverInt { get; set; }
        public List<TwinCollisionHull> CollisionHulls { get; set; }
        public Int32 AnimatedFrames { get; set; }
        public TwinDynamicSceneryAnimation Animation { get; set; }
        public Byte LodFlag { get; set; }
        public UInt32 MeshID { get; set; }
        public Vector4[] BoundingBox { get; set; }

        public TwinDynamicSceneryModel()
        {
            CollisionHulls = new List<TwinCollisionHull>();
            BoundingBox = new Vector4[2];
            Animation = new TwinDynamicSceneryAnimation();
        }

        public Int32 GetLength()
        {
            return 4 + 4 + CollisionHulls.Sum(o => o.GetLength()) + 4 + Animation.GetLength() + 1 + 4 + 2 * Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            LeftoverInt = reader.ReadInt32();
            var hullsAmount = reader.ReadInt32();
            for (var i = 0; i < hullsAmount; ++i)
            {
                var hull = new TwinCollisionHull();
                CollisionHulls.Add(hull);
                hull.Read(reader, length);
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
            writer.Write(LeftoverInt);
            writer.Write(CollisionHulls.Count);
            foreach (var hull in CollisionHulls)
            {
                hull.Write(writer);
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
