using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyInstance : BaseTwinItem, ITwinInstance
    {
        public Vector4 Position { get; set; }
        public Int32 RotationX { get; set; }
        public Int32 RotationY { get; set; }
        public Int32 RotationZ { get; set; }
        public UInt32 InstancesGrowth { get; set; }
        public List<UInt16> Instances { get; set; }
        public UInt32 PositionsGrowth { get; set; }
        public List<UInt16> Positions { get; set; }
        public UInt32 PathsGrowth { get; set; }
        public List<UInt16> Paths { get; set; }
        public UInt16 ObjectId { get; set; }
        public Int16 RefListIndex { get; set; }
        public UInt16 SpawnScriptId { get; set; }
        public Enums.InstanceState StateFlags { get; set; }
        public List<UInt32> TaggedProperties { get; set; }
        public List<Single> FloatProperties { get; set; }
        public List<Int32> IntProperties { get; set; }

        public PS2AnyInstance()
        {
            Position = new Vector4();
            Instances = new List<ushort>();
            Positions = new List<ushort>();
            Paths = new List<ushort>();
            TaggedProperties = new List<UInt32>();
            FloatProperties = new List<Single>();
            IntProperties = new List<Int32>();
        }

        public override int GetLength()
        {
            return Constants.SIZE_VECTOR4 + Constants.SIZE_UINT32 * 3 +
                12 + Instances.Count * 2 +
                12 + Positions.Count * 2 +
                12 + Paths.Count * 2 + 14 +
                4 + TaggedProperties.Count * 4 +
                4 + FloatProperties.Count * 4 +
                4 + IntProperties.Count * 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Position.Read(reader, Constants.SIZE_VECTOR4);
            RotationX = reader.ReadInt32();
            RotationY = reader.ReadInt32();
            RotationZ = reader.ReadInt32();

            // Each list is its count, its room (the same in the files) and its growth, then the IDs
            Int32 instances_cnt = reader.ReadInt32();
            reader.ReadInt32();
            InstancesGrowth = reader.ReadUInt32();
            Instances.Clear();
            for (int i = 0; i < instances_cnt; ++i)
            {
                Instances.Add(reader.ReadUInt16());
            }

            Int32 positions_cnt = reader.ReadInt32();
            reader.ReadInt32();
            PositionsGrowth = reader.ReadUInt32();
            Positions.Clear();
            for (int i = 0; i < positions_cnt; ++i)
            {
                Positions.Add(reader.ReadUInt16());
            }

            Int32 paths_cnt = reader.ReadInt32();
            reader.ReadInt32();
            PathsGrowth = reader.ReadUInt32();
            Paths.Clear();
            for (int i = 0; i < paths_cnt; ++i)
            {
                Paths.Add(reader.ReadUInt16());
            }

            ObjectId = reader.ReadUInt16();

            RefListIndex = reader.ReadInt16();
            SpawnScriptId = reader.ReadUInt16();
            // The counts of the tagged values, floats and ints, and a pad byte
            reader.ReadUInt32();
            StateFlags = (Enums.InstanceState)reader.ReadUInt32();

            Int32 taggedCount = reader.ReadInt32();
            TaggedProperties.Clear();
            for (int i = 0; i < taggedCount; ++i)
            {
                TaggedProperties.Add(reader.ReadUInt32());
            }

            Int32 floatCount = reader.ReadInt32();
            FloatProperties.Clear();
            for (int i = 0; i < floatCount; ++i)
            {
                FloatProperties.Add(reader.ReadSingle());
            }

            Int32 intCount = reader.ReadInt32();
            IntProperties.Clear();
            for (int i = 0; i < intCount; ++i)
            {
                IntProperties.Add(reader.ReadInt32());
            }
        }
        public override void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            writer.Write(RotationX);
            writer.Write(RotationY);
            writer.Write(RotationZ);

            writer.Write(Instances.Count);
            writer.Write(Instances.Count);
            writer.Write(InstancesGrowth);
            foreach (UInt16 id in Instances)
            {
                writer.Write(id);
            }

            writer.Write(Positions.Count);
            writer.Write(Positions.Count);
            writer.Write(PositionsGrowth);
            foreach (UInt16 id in Positions)
            {
                writer.Write(id);
            }

            writer.Write(Paths.Count);
            writer.Write(Paths.Count);
            writer.Write(PathsGrowth);
            foreach (UInt16 id in Paths)
            {
                writer.Write(id);
            }

            writer.Write(ObjectId);
            writer.Write(RefListIndex);
            writer.Write(SpawnScriptId);
            writer.Write((Byte)TaggedProperties.Count);
            writer.Write((Byte)FloatProperties.Count);
            writer.Write((Byte)IntProperties.Count);
            writer.Write((Byte)0);
            writer.Write((UInt32)StateFlags);

            writer.Write(TaggedProperties.Count);
            foreach (var value in TaggedProperties)
            {
                writer.Write(value);
            }

            writer.Write(FloatProperties.Count);
            foreach (var value in FloatProperties)
            {
                writer.Write(value);
            }

            writer.Write(IntProperties.Count);
            foreach (var value in IntProperties)
            {
                writer.Write(value);
            }
        }

        public override String GetName()
        {
            return $"Instance {id:X}";
        }
    }
}
