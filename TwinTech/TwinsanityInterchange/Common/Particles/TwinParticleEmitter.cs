using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    public class TwinParticleEmitter : ITwinSerializable
    {
        public UInt32 Version;
        public Vector3 Position;
        public Int16 GravityRotX;
        public Int16 GravityRotY;
        public Int16 EmitRotX;
        public Int16 EmitRotY;
        public Int16 UnkShort5;
        public Int32 Offset;
        public Char[] Name;
        public Int32 SwitchType;
        public Int32 SwitchId;
        public Single SwitchValue;
        public Int16 UnkShort6;
        public Int16 UnkShort7;
        public Single PlaneOffset;
        public Single BounceFactor;
        public Int16 GroupId;

        private Dictionary<UInt32, Int32> versionSizeMap = new Dictionary<UInt32, Int32>();
        public TwinParticleEmitter()
        {
            Position = new Vector3();
            Name = new Char[16];
            Version = 0x1E;
        }
        public TwinParticleEmitter(UInt32 ver) : this()
        {
            Version = ver;
        }

        public Int32 GetLength()
        {
            return versionSizeMap[Version];
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var basePos = reader.BaseStream.Position;
            Position.Read(reader, Constants.SIZE_VECTOR3);
            if (Version >= 0x7)
            {
                GravityRotX = reader.ReadInt16();
                GravityRotY = reader.ReadInt16();
                EmitRotX = reader.ReadInt16();
                EmitRotY = reader.ReadInt16();
            }
            else
            {
                GravityRotX = 0;
                GravityRotY = 0;
                EmitRotX = (Int16)reader.ReadInt32();
                EmitRotY = (Int16)reader.ReadInt32();
            }
            if (Version >= 0x16)
            {
                UnkShort5 = reader.ReadInt16();
            }
            if (Version >= 0x8)
            {
                Offset = reader.ReadInt32();
            }
            Name = reader.ReadChars(16);
            if (Version >= 0x9)
            {
                SwitchType = reader.ReadInt32();
                SwitchId = reader.ReadInt32();
                SwitchValue = reader.ReadSingle();
            }
            if (Version >= 0xC)
            {
                UnkShort6 = reader.ReadInt16();
                UnkShort7 = reader.ReadInt16();
                PlaneOffset = reader.ReadSingle();
            }
            BounceFactor = 0.89999998f;
            if (Version >= 0xD)
            {
                BounceFactor = reader.ReadSingle();
            }
            if (Version >= 0xF)
            {
                GroupId = reader.ReadInt16();
            }
            var sizePos = reader.BaseStream.Position;
            versionSizeMap.Add(Version, (Int32)(sizePos - basePos));
        }

        public void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            if (Version >= 0x7)
            {
                writer.Write(GravityRotX);
                writer.Write(GravityRotY);
                writer.Write(EmitRotX);
                writer.Write(EmitRotY);
            }
            else
            {
                writer.Write((Int32)EmitRotX);
                writer.Write((Int32)EmitRotY);
            }
            if (Version >= 0x16)
            {
                writer.Write(UnkShort5);
            }
            if (Version >= 0x8)
            {
                writer.Write(Offset);
            }
            writer.Write(Name, 0, 16);
            if (Version >= 0x9)
            {
                writer.Write(SwitchType);
                writer.Write(SwitchId);
                writer.Write(SwitchValue);
            }
            if (Version >= 0xC)
            {
                writer.Write(UnkShort6);
                writer.Write(UnkShort7);
                writer.Write(PlaneOffset);
            }
            if (Version >= 0xD)
            {
                writer.Write(BounceFactor);
            }
            if (Version >= 0xF)
            {
                writer.Write(GroupId);
            }
        }
    }
}
