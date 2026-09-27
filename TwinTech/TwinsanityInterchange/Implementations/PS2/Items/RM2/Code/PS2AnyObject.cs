using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code
{
    public class PS2AnyObject : BaseTwinItem, ITwinObject
    {

        Byte type;
        public ITwinObject.ObjectType Type
        {
            get => (ITwinObject.ObjectType)type;
            set => type = (Byte)value;
        }
        public Byte UnkTypeValue { get; set; }
        public Byte ReactJointAmount { get; set; }
        public Byte ExitPointAmount { get; set; }
        public String Name { get; set; }
        public List<TwinObjectTriggerBehaviour> TriggerBehaviours { get; set; }
        public List<UInt16> OGISlots { get; set; }
        public List<UInt16> AnimationSlots { get; set; }
        public List<UInt16> BehaviourSlots { get; set; }
        public List<UInt16> ObjectSlots { get; set; }
        public List<UInt16> SoundSlots { get; set; }
        public Enums.InstanceState InstanceStateFlags { get; set; }
        public List<UInt32> InstFlags { get; set; }
        public List<Single> InstFloats { get; set; }
        public List<UInt32> InstIntegers { get; set; }
        public List<UInt16> RefObjects { get; set; }
        public List<UInt16> RefOGIs { get; set; }
        public List<UInt16> RefAnimations { get; set; }
        public List<UInt16> RefCodeModels { get; set; }
        public List<UInt16> RefBehaviours { get; set; }
        public List<UInt16> RefUnknowns { get; set; }
        public List<UInt16> RefSounds { get; set; }
        public ITwinBehaviourCommandPack BehaviourPack { get; set; }

        public bool HasInstanceProperties
        {
            get
            {
                return InstFlags.Count > 0 || InstFloats.Count > 0 || InstIntegers.Count > 0;
            }
        }

        public bool ReferencesResources
        {
            get
            {
                return RefObjects.Count > 0 || RefOGIs.Count > 0 || RefAnimations.Count > 0 ||
                    RefCodeModels.Count > 0 || RefBehaviours.Count > 0 || RefUnknowns.Count > 0 ||
                    RefSounds.Count > 0;
            }
        }

        public PS2AnyObject()
        {
            TriggerBehaviours = new List<TwinObjectTriggerBehaviour>();
            OGISlots = new List<UInt16>();
            AnimationSlots = new List<UInt16>();
            BehaviourSlots = new List<UInt16>();
            ObjectSlots = new List<UInt16>();
            SoundSlots = new List<UInt16>();
            InstFlags = new List<UInt32>();
            InstFloats = new List<Single>();
            InstIntegers = new List<UInt32>();
            RefObjects = new List<UInt16>();
            RefOGIs = new List<UInt16>();
            RefAnimations = new List<UInt16>();
            RefCodeModels = new List<UInt16>();
            RefBehaviours = new List<UInt16>();
            RefUnknowns = new List<UInt16>();
            RefSounds = new List<UInt16>();
        }

        public override int GetLength()
        {
            var resourcesLength = 0;
            if (ReferencesResources)
            {
                resourcesLength += 4;
                if (RefObjects.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefObjects.Count * Constants.SIZE_UINT16;
                }
                if (RefOGIs.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefOGIs.Count * Constants.SIZE_UINT16;
                }
                if (RefAnimations.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefAnimations.Count * Constants.SIZE_UINT16;
                }
                if (RefCodeModels.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefCodeModels.Count * Constants.SIZE_UINT16;
                }
                if (RefBehaviours.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefBehaviours.Count * Constants.SIZE_UINT16;
                }
                if (RefUnknowns.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefUnknowns.Count * Constants.SIZE_UINT16;
                }
                if (RefSounds.Count > 0)
                {
                    resourcesLength += 4;
                    resourcesLength += RefSounds.Count * Constants.SIZE_UINT16;
                }
            }
            // Truly a bruh moment
            return 16 + Name.Length + 24 +
                TriggerBehaviours.Count * Constants.SIZE_UINT32 + OGISlots.Count * Constants.SIZE_UINT16 +
                AnimationSlots.Count * Constants.SIZE_UINT16 + BehaviourSlots.Count * Constants.SIZE_UINT16 +
                ObjectSlots.Count * Constants.SIZE_UINT16 + SoundSlots.Count * Constants.SIZE_UINT16 +
                (HasInstanceProperties ? 20 + InstFlags.Count * Constants.SIZE_UINT32 + InstFloats.Count * 4 +
                InstIntegers.Count * Constants.SIZE_UINT32 : 0) + resourcesLength + BehaviourPack.GetLength();
        }

        public override void ComputeHash(Stream stream, UInt32 length)
        {
            var startPos = stream.Position;
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            var reader = new BinaryReader(stream);
            Read(reader, (Int32)length);
            WriteInternal(writer, writeName: false);
            ms.Position = 0;
            base.ComputeHash(ms, (UInt32)ms.Length);

            stream.Position = startPos;
        }

        protected virtual ITwinBehaviourCommandPack CreateCommandPack()
        {
            return new PS2BehaviourCommandPack();
        }

        public override void Read(BinaryReader reader, int length)
        {
            var bitfield = reader.ReadUInt32();
            type = (Byte)(bitfield >> 0x14 & 0xFF);
            UnkTypeValue = (Byte)(bitfield >> 0xC & 0xFF);
            ReactJointAmount = (Byte)(bitfield >> 0x6 & 0x3F);
            ExitPointAmount = (Byte)(bitfield & 0x3F);

            var hasInstProps = (bitfield & 0x20000000) != 0;
            var refRes = (bitfield & 0x40000000) != 0;
            // Slots map skipped
            for (var i = 0; i < 8; ++i)
            {
                reader.ReadByte();
            }
            var strLen = reader.ReadInt32();
            Name = new String(reader.ReadChars(strLen));

            // Read trigger behaviours
            {
                var amount = reader.ReadInt32();
                TriggerBehaviours.Clear();
                for (var i = 0; i < amount; ++i)
                {
                    TriggerBehaviours.Add(new TwinObjectTriggerBehaviour(reader.ReadUInt32()));
                }
            }
            FillResourceList(reader, OGISlots);
            FillResourceList(reader, AnimationSlots);
            FillResourceList(reader, BehaviourSlots);
            FillResourceList(reader, ObjectSlots);
            FillResourceList(reader, SoundSlots);

            if (hasInstProps)
            {
                reader.ReadUInt32();
                InstanceStateFlags = (Enums.InstanceState)reader.ReadUInt32();
                FillResourceList(reader, InstFlags, true);
                // Sadly this is the only one not fitting into UI32 or UI16, smh too lazy to create a Type to BinaryReader mapper :^)
                var amount = reader.ReadInt32();
                InstFloats.Clear();
                for (var i = 0; i < amount; ++i)
                {
                    InstFloats.Add(reader.ReadSingle());
                }
                FillResourceList(reader, InstIntegers, true);
            }

            if (refRes)
            {
                var resources = (ITwinObject.ResourcesBitfield)reader.ReadUInt32();
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.OBJECTS))
                {
                    FillResourceList(reader, RefObjects);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.OGIS))
                {
                    FillResourceList(reader, RefOGIs);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.ANIMATIONS))
                {
                    FillResourceList(reader, RefAnimations);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.CODE_MODELS))
                {
                    FillResourceList(reader, RefCodeModels);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.SCRIPTS))
                {
                    FillResourceList(reader, RefBehaviours);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.UNKNOWN))
                {
                    FillResourceList(reader, RefUnknowns);
                }
                if (resources.HasFlag(ITwinObject.ResourcesBitfield.SOUNDS))
                {
                    FillResourceList(reader, RefSounds);
                }
            }
            BehaviourPack = CreateCommandPack();
            BehaviourPack.Read(reader, length);
        }

        public override void Write(BinaryWriter writer)
        {
            WriteInternal(writer);
        }

        private void WriteInternal(BinaryWriter writer, bool writeName = true)
        {
            UInt32 newBitfield = ExitPointAmount;
            if (ReferencesResources)
            {
                newBitfield |= 0x40000000;
            }
            if (HasInstanceProperties)
            {
                // If object has instance properties it means we have to mark that the instance properties have also been loaded
                newBitfield |= 0x30000000;
            }
            UInt32 objType = (UInt32)(type << 0x14);
            UInt32 objTypeRelVal = (UInt32)(UnkTypeValue << 0xC);
            UInt32 unkOgiArraySize = (UInt32)((ReactJointAmount & 0x3F) << 0x6);
            newBitfield |= objType;
            newBitfield |= objTypeRelVal;
            newBitfield |= unkOgiArraySize;
            writer.Write(newBitfield);
            var slotsMap = new Byte[8];
            Debug.Assert(OGISlots.Count == AnimationSlots.Count, "Amount of slots of OGIs and Animations must be equal");
            slotsMap[0] = (Byte)OGISlots.Count;
            slotsMap[1] = (Byte)BehaviourSlots.Count;
            slotsMap[2] = (Byte)ObjectSlots.Count;
            slotsMap[3] = (Byte)TriggerBehaviours.Count;
            slotsMap[4] = (Byte)SoundSlots.Count;
            slotsMap[5] = 0;
            slotsMap[6] = 0;
            slotsMap[7] = 0;

            writer.Write(slotsMap);
            if (writeName)
            {
                writer.Write(Name.Length);
                writer.Write(Name.ToCharArray(), 0, Name.Length);
            }

            {
                writer.Write(TriggerBehaviours.Count);
                foreach (var beh in TriggerBehaviours)
                {
                    writer.Write(beh.Compress());
                }
            }
            WriteResourceList(writer, OGISlots);
            WriteResourceList(writer, AnimationSlots);
            WriteResourceList(writer, BehaviourSlots);
            WriteResourceList(writer, ObjectSlots);
            WriteResourceList(writer, SoundSlots);

            if (HasInstanceProperties)
            {
                writer.Write((Byte)InstFlags.Count);
                writer.Write((Byte)InstFloats.Count);
                writer.Write((Byte)InstIntegers.Count);
                writer.Write((Byte)0);
                writer.Write((UInt32)InstanceStateFlags);
                WriteResourceList(writer, InstFlags, true);
                writer.Write(InstFloats.Count);
                for (var i = 0; i < InstFloats.Count; ++i)
                {
                    writer.Write(InstFloats[i]);
                }
                WriteResourceList(writer, InstIntegers, true);
            }

            if (ReferencesResources)
            {
                ITwinObject.ResourcesBitfield newResources = new();
                if (RefObjects.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.OBJECTS;
                }
                if (RefOGIs.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.OGIS;
                }
                if (RefAnimations.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.ANIMATIONS;
                }
                if (RefCodeModels.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.CODE_MODELS;
                }
                if (RefBehaviours.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.SCRIPTS;
                }
                if (RefUnknowns.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.UNKNOWN;
                }
                if (RefSounds.Count > 0)
                {
                    newResources |= ITwinObject.ResourcesBitfield.SOUNDS;
                }
                writer.Write((UInt32)newResources);
                if (RefObjects.Count > 0)
                {
                    WriteResourceList(writer, RefObjects);
                }
                if (RefOGIs.Count > 0)
                {
                    WriteResourceList(writer, RefOGIs);
                }
                if (RefAnimations.Count > 0)
                {
                    WriteResourceList(writer, RefAnimations);
                }
                if (RefCodeModels.Count > 0)
                {
                    WriteResourceList(writer, RefCodeModels);
                }
                if (RefBehaviours.Count > 0)
                {
                    WriteResourceList(writer, RefBehaviours);
                }
                if (RefUnknowns.Count > 0)
                {
                    WriteResourceList(writer, RefUnknowns);
                }
                if (RefSounds.Count > 0)
                {
                    WriteResourceList(writer, RefSounds);
                }
            }
            BehaviourPack.Write(writer);
        }

        private void FillResourceList(BinaryReader reader, IList list, bool UI32 = false)
        {
            var amount = reader.ReadInt32();
            list.Clear();
            for (var i = 0; i < amount; ++i)
            {
                if (UI32)
                {
                    list.Add(reader.ReadUInt32());
                }
                else
                {
                    list.Add(reader.ReadUInt16());
                }
            }
        }

        private void WriteResourceList(BinaryWriter writer, IList list, bool UI32 = false)
        {
            writer.Write(list.Count);
            for (var i = 0; i < list.Count; ++i)
            {
                if (UI32)
                {
                    writer.Write((UInt32)list[i]);
                }
                else
                {
                    writer.Write((UInt16)list[i]);
                }
            }
        }

        public override String GetName()
        {
            return $"{Name.Replace("|", "_")}_{id:X}";
        }
    }
}
