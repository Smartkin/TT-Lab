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
        public Vector4 Rotation { get; set; }
        public TwinIntegerRotation RotationX { get; set; }
        public TwinIntegerRotation RotationY { get; set; }
        public TwinIntegerRotation RotationZ { get; set; }
        public UInt32 InstancesRelated { get; set; }
        public List<UInt16> Instances { get; set; }
        public UInt32 PositionsRelated { get; set; }
        public List<UInt16> Positions { get; set; }
        public UInt32 PathsRelated { get; set; }
        public List<UInt16> Paths { get; set; }
        public UInt16 ObjectId { get; set; }
        public Int16 RefListIndex { get; set; }
        public UInt16 OnSpawnHeaderScriptID { get; set; }
        public UInt32 StateFlags { get; set; }
        public List<UInt32> ParamList1 { get; set; }
        public List<Single> ParamList2 { get; set; }
        public List<UInt32> ParamList3 { get; set; }

        public PS2AnyInstance()
        {
            Position = new Vector4();
            Rotation = new Vector4(0, 0, 0, 1);
            RotationX = new TwinIntegerRotation();
            RotationY = new TwinIntegerRotation();
            RotationZ = new TwinIntegerRotation();
            Instances = new List<ushort>();
            Positions = new List<ushort>();
            Paths = new List<ushort>();
            ParamList1 = new List<uint>();
            ParamList2 = new List<float>();
            ParamList3 = new List<uint>();
        }

        public override int GetLength()
        {
            return Constants.SIZE_VECTOR4 + Constants.SIZE_UINT32 * 3 +
                12 + Instances.Count * 2 +
                12 + Positions.Count * 2 +
                12 + Paths.Count * 2 + 14 +
                4 + ParamList1.Count * 4 +
                4 + ParamList2.Count * 4 +
                4 + ParamList3.Count * 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Position.Read(reader, Constants.SIZE_VECTOR4);
            RotationX.Read(reader, Constants.SIZE_UINT32);
            RotationY.Read(reader, Constants.SIZE_UINT32);
            RotationZ.Read(reader, Constants.SIZE_UINT32);

            reader.ReadInt32();
            Int32 instances_cnt = reader.ReadInt32();
            InstancesRelated = reader.ReadUInt32();
            Instances.Clear();
            for (int i = 0; i < instances_cnt; ++i)
            {
                Instances.Add(reader.ReadUInt16());
            }

            reader.ReadInt32();
            Int32 positions_cnt = reader.ReadInt32();
            PositionsRelated = reader.ReadUInt32();
            Positions.Clear();
            for (int i = 0; i < positions_cnt; ++i)
            {
                Positions.Add(reader.ReadUInt16());
            }

            reader.ReadInt32();
            Int32 paths_cnt = reader.ReadInt32();
            PathsRelated = reader.ReadUInt32();
            Paths.Clear();
            for (int i = 0; i < paths_cnt; ++i)
            {
                Paths.Add(reader.ReadUInt16());
            }

            ObjectId = reader.ReadUInt16();

            RefListIndex = reader.ReadInt16();
            OnSpawnHeaderScriptID = reader.ReadUInt16();
            // Flags, floats and ints amount + pad byte
            reader.ReadUInt32();
            StateFlags = reader.ReadUInt32();

            Int32 param1_cnt = reader.ReadInt32();
            ParamList1.Clear();
            for (int i = 0; i < param1_cnt; ++i)
            {
                ParamList1.Add(reader.ReadUInt32());
            }

            Int32 param2_cnt = reader.ReadInt32();
            ParamList2.Clear();
            for (int i = 0; i < param2_cnt; ++i)
            {
                ParamList2.Add(reader.ReadSingle());
            }

            Int32 param3_cnt = reader.ReadInt32();
            ParamList3.Clear();
            for (int i = 0; i < param3_cnt; ++i)
            {
                ParamList3.Add(reader.ReadUInt32());
            }
        }
        public override void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            RotationX.Write(writer);
            RotationY.Write(writer);
            RotationZ.Write(writer);

            writer.Write(Instances.Count);
            writer.Write(Instances.Count);
            writer.Write(InstancesRelated);
            foreach (UInt16 id in Instances)
            {
                writer.Write(id);
            }

            writer.Write(Positions.Count);
            writer.Write(Positions.Count);
            writer.Write(PositionsRelated);
            foreach (UInt16 id in Positions)
            {
                writer.Write(id);
            }

            writer.Write(Paths.Count);
            writer.Write(Paths.Count);
            writer.Write(PathsRelated);
            foreach (UInt16 id in Paths)
            {
                writer.Write(id);
            }

            writer.Write(ObjectId);
            writer.Write(RefListIndex);
            writer.Write(OnSpawnHeaderScriptID);
            writer.Write((Byte)ParamList1.Count);
            writer.Write((Byte)ParamList2.Count);
            writer.Write((Byte)ParamList3.Count);
            writer.Write((Byte)0);
            writer.Write(StateFlags);

            writer.Write(ParamList1.Count);
            foreach (UInt32 id in ParamList1)
            {
                writer.Write(id);
            }

            writer.Write(ParamList2.Count);
            foreach (Single id in ParamList2)
            {
                writer.Write(id);
            }

            writer.Write(ParamList3.Count);
            foreach (UInt32 id in ParamList3)
            {
                writer.Write(id);
            }
        }

        public override String GetName()
        {
            return $"Instance {id:X}";
        }
    }
}
