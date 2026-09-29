using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyTemplate : BaseTwinItem, ITwinTemplate
    {
        public String Name { get; set; }
        public UInt16 ObjectId { get; set; }
        public Byte ObjectSubType { get; set; }
        public Byte ObjectType { get; set; }
        public List<UInt16> BehaviourStarters { get; set; }
        public UInt32 BehaviourListCapacity { get; set; }
        public UInt32 BehaviourListGrowth { get; set; }
        public Byte ObjectExitPoints { get; set; }
        public Byte ObjectReactJoints { get; set; }
        public Enums.InstanceState InstanceStateFlags { get; set; }
        public List<UInt32> Flags { get; set; }
        public List<Single> Floats { get; set; }
        public List<UInt32> Ints { get; set; }

        public PS2AnyTemplate()
        {
            BehaviourStarters = new List<ushort>();
            BehaviourListGrowth = 10;
            Floats = new List<float>();
            Ints = new List<uint>();
            Flags = new List<uint>();
        }

        public override int GetLength()
        {
            return 4 + Name.Length + 16 + BehaviourStarters.Count * 2 + 22 + Flags.Count * 4 + Floats.Count * 4 + Ints.Count * 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Int32 NameLen = reader.ReadInt32();
            Name = new string(reader.ReadChars(NameLen));
            ObjectId = reader.ReadUInt16();
            ObjectSubType = reader.ReadByte();
            ObjectType = reader.ReadByte();
            var amt = reader.ReadUInt32();
            BehaviourListCapacity = reader.ReadUInt32();
            BehaviourListGrowth = reader.ReadUInt32();
            BehaviourStarters.Clear();
            for (var i = 0; i < amt; ++i)
            {
                BehaviourStarters.Add(reader.ReadUInt16());
            }
            ObjectExitPoints = reader.ReadByte();
            ObjectReactJoints = reader.ReadByte();
            // The properties' header packs their amounts, which the lists give again
            reader.ReadUInt32();
            InstanceStateFlags = (Enums.InstanceState)reader.ReadUInt32();
            Int32 flags = reader.ReadInt32();
            Flags.Clear();
            for (int i = 0; i < flags; ++i)
            {
                Flags.Add(reader.ReadUInt32());
            }
            Int32 floats = reader.ReadInt32();
            Floats.Clear();
            for (int i = 0; i < floats; ++i)
            {
                Floats.Add(reader.ReadSingle());
            }
            Int32 ints = reader.ReadInt32();
            Ints.Clear();
            for (int i = 0; i < ints; ++i)
            {
                Ints.Add(reader.ReadUInt32());
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Name.Length);
            writer.Write(Name.ToCharArray());
            writer.Write(ObjectId);
            writer.Write(ObjectSubType);
            writer.Write(ObjectType);
            writer.Write(BehaviourStarters.Count);
            writer.Write(BehaviourListCapacity);
            writer.Write(BehaviourListGrowth);
            foreach (var s in BehaviourStarters)
            {
                writer.Write(s);
            }
            writer.Write(ObjectExitPoints);
            writer.Write(ObjectReactJoints);
            writer.Write(PropertiesHeader(Flags.Count, Floats.Count, Ints.Count));
            writer.Write((UInt32)InstanceStateFlags);
            writer.Write(Flags.Count);
            foreach (UInt32 e in Flags)
            {
                writer.Write(e);
            }
            writer.Write(Floats.Count);
            foreach (Single e in Floats)
            {
                writer.Write(e);
            }
            writer.Write(Ints.Count);
            foreach (UInt32 e in Ints)
            {
                writer.Write(e);
            }
        }

        /// <summary>
        /// The instance properties' header: the amounts of flags, floats and ints as bytes (InstanceProperties 0x0-0x2)
        /// </summary>
        public static UInt32 PropertiesHeader(Int32 flags, Int32 floats, Int32 ints)
        {
            return (UInt32)(flags & 0xFF) | (UInt32)(floats & 0xFF) << 8 | (UInt32)(ints & 0xFF) << 16;
        }

        public override String GetName()
        {
            return Name;
        }
    }
}
