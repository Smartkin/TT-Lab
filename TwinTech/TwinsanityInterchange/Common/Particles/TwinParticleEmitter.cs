using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    /// <summary>
    /// An emitter placed in a chunk playing one of its or the default chunk's systems. Worked out from the PAL executable's
    /// section reader (ReadMainParticleSection 0x1b90a0, a 0x50 byte record) and the instance creation (0x1b8dd8). Rotations
    /// are in 65536ths of a turn and applied tilt (about Z, the up vector leaning towards -X), then yaw (about Y), then roll
    /// (about X). The game's data only sets the emit tilt and yaw
    /// </summary>
    public class TwinParticleEmitter : ITwinSerializable
    {
        public UInt32 Version;
        /// <summary>
        /// 0x00, in the chunk
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// 0x14, turns the space the particles move and fall in (their gravity is along its up), and the emit rotation with it
        /// </summary>
        public Int16 GravityTilt;
        /// <summary>
        /// 0x16, see <see cref="GravityTilt"/>
        /// </summary>
        public Int16 GravityYaw;
        /// <summary>
        /// 0x18, turns the direction the particles are emitted along, within the gravity space
        /// </summary>
        public Int16 EmitTilt;
        /// <summary>
        /// 0x1a, see <see cref="EmitTilt"/>
        /// </summary>
        public Int16 EmitYaw;
        /// <summary>
        /// 0x1c, a roll about X applied after the tilt and yaw (version 0x16 on)
        /// </summary>
        public Int16 EmitRoll;
        /// <summary>
        /// 0x20, frames the emitter's on and off cycle is shifted by against the game's frame counter (on top of the system's),
        /// and where a rotor system starts its sweep
        /// </summary>
        public Int32 TimingOffset;
        /// <summary>
        /// 0x24, the system it plays, its chunk's first and then the default chunk's
        /// </summary>
        public Char[] Name;
        /// <summary>
        /// 0x34, never read by the retail game (0 in its data)
        /// </summary>
        public Int32 SwitchType;
        /// <summary>
        /// 0x38, never read by the retail game (-1 in its data)
        /// </summary>
        public Int32 SwitchId;
        /// <summary>
        /// 0x3c, never read by the retail game (0 in its data)
        /// </summary>
        public Single SwitchValue;
        /// <summary>
        /// 0x40, never read by the retail game (0 in its data)
        /// </summary>
        public Int16 UnusedShort;
        /// <summary>
        /// 0x42, the angle about Y of the vertical plane <see cref="TwinParticleSystem.GenSortType.BounceXZ"/> particles bounce off
        /// </summary>
        public Int16 BouncePlaneAngle;
        /// <summary>
        /// 0x44, the height of the plane <see cref="TwinParticleSystem.GenSortType.Bounce"/> particles bounce off (or the
        /// vertical plane's distance), relative to the emitter
        /// </summary>
        public Single PlaneOffset;
        /// <summary>
        /// 0x48, how much of the speed a bounce keeps, 0.9 by default
        /// </summary>
        public Single BounceFactor;
        /// <summary>
        /// 0x4c, never read by the retail game (0 in its data)
        /// </summary>
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
                GravityTilt = reader.ReadInt16();
                GravityYaw = reader.ReadInt16();
                EmitTilt = reader.ReadInt16();
                EmitYaw = reader.ReadInt16();
            }
            else
            {
                GravityTilt = 0;
                GravityYaw = 0;
                EmitTilt = (Int16)reader.ReadInt32();
                EmitYaw = (Int16)reader.ReadInt32();
            }
            if (Version >= 0x16)
            {
                EmitRoll = reader.ReadInt16();
            }
            if (Version >= 0x8)
            {
                TimingOffset = reader.ReadInt32();
            }
            Name = GameText.ReadChars(reader, 16);
            if (Version >= 0x9)
            {
                SwitchType = reader.ReadInt32();
                SwitchId = reader.ReadInt32();
                SwitchValue = reader.ReadSingle();
            }
            else
            {
                SwitchId = -1;
            }
            if (Version >= 0xC)
            {
                UnusedShort = reader.ReadInt16();
                BouncePlaneAngle = reader.ReadInt16();
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
            versionSizeMap[Version] = (Int32)(sizePos - basePos);
        }

        public void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            if (Version >= 0x7)
            {
                writer.Write(GravityTilt);
                writer.Write(GravityYaw);
                writer.Write(EmitTilt);
                writer.Write(EmitYaw);
            }
            else
            {
                writer.Write((Int32)EmitTilt);
                writer.Write((Int32)EmitYaw);
            }
            if (Version >= 0x16)
            {
                writer.Write(EmitRoll);
            }
            if (Version >= 0x8)
            {
                writer.Write(TimingOffset);
            }
            GameText.Write(writer, Name, 0, 16);
            if (Version >= 0x9)
            {
                writer.Write(SwitchType);
                writer.Write(SwitchId);
                writer.Write(SwitchValue);
            }
            if (Version >= 0xC)
            {
                writer.Write(UnusedShort);
                writer.Write(BouncePlaneAngle);
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
