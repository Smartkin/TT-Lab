using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Common
{
    /// <summary>
    /// The box every trigger and camera is placed by (verified in the PAL executable: ReadBaseTrigger 0x26eb88, FUN_001f6028,
    /// InitTriggerNode 0x2081c0, FUN_001f5428)
    /// </summary>
    public class TwinTrigger : ITwinSerializable
    {
        /// <summary>
        /// The trigger's flags. The low byte is the kind the tools gave it (50 on most, 60, 65, 5 and 100 on a few), the game only
        /// tells 0 apart: such a trigger becomes a plain box of its chunk (up to 7) the sound code tests the player against instead
        /// of a trigger node. Bits 8-11 say which of the 4 messages are sent (<see cref="TriggerFlags"/>), bit 12 stops the
        /// game from polling the trigger's box, the rest is never read
        /// </summary>
        public UInt32 Header { get; set; }
        public TriggerActivatorObjects ObjectActivatorMask { get; set; }
        /// <summary>
        /// Seconds between two checks of what's inside the box (0.3 on nearly every retail trigger, cameras mostly leave it 0),
        /// stored in ticks by the trigger's node
        /// </summary>
        public Single CheckInterval { get; set; }
        /// <summary>
        /// The instance list's growth step from the tools' list header (always 10), the game keeps it and never reads it
        /// </summary>
        public UInt32 InstanceExtensionValue { get; set; }
        public Vector4 Rotation { get; set; }
        public Vector4 Position { get; set; }
        public Vector4 Scale { get; set; }
        public List<UInt16> Instances { get; }
        public TwinTrigger()
        {
            Rotation = new Vector4();
            Position = new Vector4();
            Scale = new Vector4();
            Instances = new List<UInt16>();
        }

        public int GetLength()
        {
            return 12 + Position.GetLength() + Rotation.GetLength() + Scale.GetLength() + 12 + Instances.Count * Constants.SIZE_UINT16;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            Header = reader.ReadUInt32();
            ObjectActivatorMask = (TriggerActivatorObjects)reader.ReadUInt32();
            CheckInterval = reader.ReadSingle();
            Rotation.Read(reader, Constants.SIZE_VECTOR4);
            Position.Read(reader, Constants.SIZE_VECTOR4);
            Scale.Read(reader, Constants.SIZE_VECTOR4);
            reader.ReadUInt32(); // instances amount
            UInt32 instances_cnt = reader.ReadUInt32();
            InstanceExtensionValue = reader.ReadUInt32();
            Instances.Clear();
            for (int i = 0; i < instances_cnt; ++i)
            {
                Instances.Add(reader.ReadUInt16());
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write((UInt32)ObjectActivatorMask);
            writer.Write(CheckInterval);
            Rotation.Write(writer);
            Position.Write(writer);
            Scale.Write(writer);
            writer.Write(Instances.Count);
            writer.Write(Instances.Count);
            writer.Write(InstanceExtensionValue);
            for (int i = 0; i < Instances.Count; ++i)
            {
                writer.Write(Instances[i]);
            }
        }
    }
}
