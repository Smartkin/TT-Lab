using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// A joint of an OGI (the game's JointStruct, ReadJoint 0x297x): stored as words, of which the game keeps a byte each
    /// </summary>
    public class TwinJoint : ITwinSerializable
    {
        /// <summary>
        /// The joint's ID, 255 for none. The joints with IDs are the ones the game finds by ID (an animation's progress on a joint,
        /// the callbacks taking part in a joint's animation), the OGI's header counts them
        /// </summary>
        public Int32 Id { get; set; }
        public Int32 Index { get; set; }
        public Int32 ParentIndex { get; set; }
        /// <summary>
        /// How many children the joint has. The game keeps its low 4 bits as the low half of the joint's detail byte and never reads them
        /// </summary>
        public Int32 ChildCount { get; set; }
        /// <summary>
        /// The level of detail below which the joint's children aren't animated (the high half of its detail byte). Every retail caller
        /// animates with detail 0, so it never leaves any out
        /// </summary>
        public Int32 Detail { get; set; }
        public Vector4 LocalTranslation { get; set; }
        public Vector4 WorldTranslation { get; set; }
        public Vector4 LocalRotation { get; set; }
        public Vector4 UnusedRotation { get; set; }
        public Vector4 AdditionalAnimationRotation { get; set; }

        public TwinJoint()
        {
            LocalTranslation = new Vector4();
            WorldTranslation = new Vector4();
            LocalRotation = new Vector4();
            UnusedRotation = new Vector4();
            AdditionalAnimationRotation = new Vector4();
        }
        public int GetLength()
        {
            return Constants.SIZE_JOINT;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            Id = (Int32)(reader.ReadUInt32() & 0xFF);
            Index = (Int32)(reader.ReadUInt32() & 0xFF);
            ParentIndex = (Int32)(reader.ReadUInt32() & 0xFF);
            ChildCount = (Int32)(reader.ReadUInt32() & 0xFF);
            Detail = (Int32)(reader.ReadUInt32() & 0xFF);
            LocalTranslation.Read(reader, Constants.SIZE_VECTOR4);
            WorldTranslation.Read(reader, Constants.SIZE_VECTOR4);
            LocalRotation.Read(reader, Constants.SIZE_VECTOR4);
            UnusedRotation.Read(reader, Constants.SIZE_VECTOR4);
            AdditionalAnimationRotation.Read(reader, Constants.SIZE_VECTOR4);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Index);
            writer.Write(ParentIndex);
            writer.Write(ChildCount);
            writer.Write(Detail);
            LocalTranslation.Write(writer);
            WorldTranslation.Write(writer);
            LocalRotation.Write(writer);
            UnusedRotation.Write(writer);
            AdditionalAnimationRotation.Write(writer);
        }
    }
}
