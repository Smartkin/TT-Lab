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
        public List<UInt32> TaggedProperties { get; set; }
        public List<Single> FloatProperties { get; set; }
        public List<Int32> IntProperties { get; set; }

        public PS2AnyTemplate()
        {
            BehaviourStarters = new List<ushort>();
            BehaviourListGrowth = 10;
            FloatProperties = new List<Single>();
            IntProperties = new List<Int32>();
            TaggedProperties = new List<UInt32>();
        }

        public override int GetLength()
        {
            return 4 + Name.Length + 16 + BehaviourStarters.Count * 2 + 22 + TaggedProperties.Count * 4 + FloatProperties.Count * 4 + IntProperties.Count * 4;
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
            Int32 tagged = reader.ReadInt32();
            TaggedProperties.Clear();
            for (int i = 0; i < tagged; ++i)
            {
                TaggedProperties.Add(reader.ReadUInt32());
            }
            Int32 floats = reader.ReadInt32();
            FloatProperties.Clear();
            for (int i = 0; i < floats; ++i)
            {
                FloatProperties.Add(reader.ReadSingle());
            }
            Int32 ints = reader.ReadInt32();
            IntProperties.Clear();
            for (int i = 0; i < ints; ++i)
            {
                IntProperties.Add(reader.ReadInt32());
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
            writer.Write(PropertiesHeader(TaggedProperties.Count, FloatProperties.Count, IntProperties.Count));
            writer.Write((UInt32)InstanceStateFlags);
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

        /// <summary>
        /// The instance properties' header: the counts of tagged values, floats and ints as bytes (the game's PropertyList::counts)
        /// </summary>
        public static UInt32 PropertiesHeader(Int32 tagged, Int32 floats, Int32 ints)
        {
            return (UInt32)(tagged & 0xFF) | (UInt32)(floats & 0xFF) << 8 | (UInt32)(ints & 0xFF) << 16;
        }

        public override String GetName()
        {
            return Name;
        }
    }
}
