using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Particles
{
    public class TwinParticleSystem : ITwinSerializable
    {
        public UInt32 Version;
        public Char[] Name;
        public Byte UnkByte1;
        public UInt16 GenRate;
        public UInt16 MaxParticleCount;
        public UInt16 UnkUShort3;
        public UInt16 EmitterOverTime;
        public UInt16 EmitterOverTimeRandom;
        public UInt16 EmitterOffTime;
        public UInt16 EmitterOffTimeRandom;
        public Byte GenSort;
        public Byte UnkByte3;
        public Byte TextureFilter;
        public Byte UnkByte5;
        public Single UnkFloat1;
        public Single CutOnRadius;
        public Single CutOffRadius;
        public Single DrawCutOff;
        public Single UnkFloat5;
        public Single UnkFloat6;
        public Single Velocity;
        public Vector3 RandomEmit;
        public Vector3 RandomStart;
        public Single UnkFloat8;
        public Single UnkFloat9;
        public Single UnkFloat10;
        public Single UnkFloat11;
        public Single UnkFloat12;
        public Single UnkFloat13;
        public Single UnkFloat14;
        public Single UnkFloat15;
        public Single UnkFloat16;
        public Single UnkFloat17;
        public Single UnkFloat18;
        public Single UnkFloat19;
        public Single Gravity;
        public Single ParticleLifeTime;
        public UInt16 UnkUShort8;
        public Byte UnkByte6;
        public Byte UnkByte7;
        public Single UnkFloat22;
        public Single JibberXFreq;
        public Single JibberXAmp;
        public Single JibberYFreq;
        public Single JibberYAmp;
        public Vector4[] ColorGradients;
        public Vector2[] AlphaGradient;
        public Vector2 Distortion;
        public Single MinSize;
        public Single MaxSize;
        public Vector2[] SizeWidth;
        public Vector2[] SizeHeight;
        public Single MinRotation;
        public Single MaxRotation;
        public Vector2[] Rotation;
        public Vector2[] UnkGradient1;
        public Vector2[] UnkGradient2;
        public Vector2 TextureStart;
        public Vector2 TextureEnd;
        public Vector2[] Collision;
        public Byte CollisionNumSpheres;
        public Byte DrawFlag;
        private Int32 padAmount;
        public Single ScaleFactor;
        public Int16 ParticleGhostsNum;
        public Single GhostSeparation;
        public Int16 StarRadialPoints;
        public Single StarRadiusRatio;
        public Single RampTime;
        public Int32 TexturePage;
        public Vector4 UnkVec3;

        private readonly Dictionary<UInt32, Int32> versionSizeMap = new();
        public TwinParticleSystem()
        {
            Name = new Char[16];
            RandomEmit = new Vector3();
            RandomStart = new Vector3();
            ColorGradients = new Vector4[8];
            AlphaGradient = new Vector2[8];
            SizeWidth = new Vector2[8];
            SizeHeight = new Vector2[8];
            Rotation = new Vector2[8];
            UnkGradient1 = new Vector2[8];
            UnkGradient2 = new Vector2[8];
            Collision = new Vector2[8];
            UnkVec3 = new Vector4();
            Version = 0x1E;
        }
        public TwinParticleSystem(UInt32 ver) : this()
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

            Name = reader.ReadChars(16);
            if (Version == 0x20)
            {
                reader.ReadByte();
                UnkByte1 = reader.ReadByte();
            }
            GenRate = reader.ReadUInt16();
            MaxParticleCount = reader.ReadUInt16();
            UnkUShort3 = reader.ReadUInt16();
            EmitterOverTime = reader.ReadUInt16();
            EmitterOverTimeRandom = reader.ReadUInt16();
            EmitterOffTime = reader.ReadUInt16();
            EmitterOffTimeRandom = reader.ReadUInt16();
            GenSort = reader.ReadByte();
            UnkByte3 = reader.ReadByte();
            TextureFilter = reader.ReadByte();
            UnkByte5 = reader.ReadByte();
            UnkFloat1 = reader.ReadSingle();
            if (Version == 0x20)
            {
                UnkFloat1 = 25;
            }
            CutOnRadius = 0;
            CutOffRadius = 25;
            if (Version >= 0x6)
            {
                CutOnRadius = reader.ReadSingle();
                CutOffRadius = reader.ReadSingle();
            }
            DrawCutOff = 0;
            if (Version >= 0xA)
            {
                DrawCutOff = reader.ReadSingle();
                if (DrawCutOff <= 2)
                {
                    DrawCutOff = 999999.875f;
                }
            }
            if (Version <= 0x16 || Version == 0x20)
            {
                UnkFloat5 = 0;
            }
            else
            {
                UnkFloat5 = reader.ReadSingle();
            }
            if (Version < 0x18 || Version == 0x20)
            {
                UnkFloat6 = 0.5f;
            }
            else
            {
                UnkFloat6 = reader.ReadSingle();
            }
            if (Version < 0x7)
            {
                reader.ReadBytes(8);
            }
            Velocity = reader.ReadSingle();
            RandomEmit.Read(reader, Constants.SIZE_VECTOR3);
            if (Version < 0x12)
            {
                reader.ReadBytes(0xC);
            }
            RandomStart.Read(reader, Constants.SIZE_VECTOR3);
            if (Version < 0x12)
            {
                reader.ReadBytes(0xC);
            }
            UnkFloat8 = reader.ReadSingle();
            UnkFloat9 = reader.ReadSingle();
            UnkFloat10 = reader.ReadSingle();
            UnkFloat11 = reader.ReadSingle();
            UnkFloat12 = reader.ReadSingle();
            UnkFloat13 = reader.ReadSingle();
            UnkFloat14 = reader.ReadSingle();
            UnkFloat15 = reader.ReadSingle();
            UnkFloat16 = reader.ReadSingle();
            UnkFloat17 = reader.ReadSingle();
            UnkFloat18 = reader.ReadSingle();
            UnkFloat19 = reader.ReadSingle();
            Gravity = reader.ReadSingle();
            ParticleLifeTime = reader.ReadSingle();
            UnkUShort8 = reader.ReadUInt16();
            UnkByte6 = reader.ReadByte();
            UnkByte7 = reader.ReadByte();
            UnkFloat22 = reader.ReadSingle();
            JibberXFreq = reader.ReadSingle();
            JibberXAmp = reader.ReadSingle();
            JibberYFreq = reader.ReadSingle();
            JibberYAmp = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                ColorGradients[i] = new Vector4();
                ColorGradients[i].Read(reader, Constants.SIZE_VECTOR4);
            }
            for (var i = 0; i < 8; ++i)
            {
                AlphaGradient[i] = new Vector2();
                AlphaGradient[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            Distortion = new Vector2
            {
                X = 0.125f,
                Y = 0.125f
            };
            if (Version > 0x15)
            {
                Distortion.Read(reader, Constants.SIZE_VECTOR2);
            }
            MinSize = reader.ReadSingle();
            MaxSize = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                SizeWidth[i] = new Vector2();
                SizeWidth[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                SizeHeight[i] = new Vector2();
                SizeHeight[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            MinRotation = reader.ReadSingle();
            MaxRotation = reader.ReadSingle();
            for (var i = 0; i < 8; ++i)
            {
                Rotation[i] = new Vector2();
                Rotation[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnkGradient1[i] = new Vector2();
                UnkGradient1[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnkGradient2[i] = new Vector2();
                UnkGradient2[i].Read(reader, Constants.SIZE_VECTOR2);
            }
            TextureStart = new Vector2();
            TextureStart.Read(reader, Constants.SIZE_VECTOR2);
            TextureEnd = new Vector2();
            TextureEnd.Read(reader, Constants.SIZE_VECTOR2);
            if (Version == 0x20)
            {
                reader.ReadBytes(4);
            }
            if (Version >= 0x3)
            {
                for (var i = 0; i < 8; ++i)
                {
                    Collision[i] = new Vector2();
                    Collision[i].Read(reader, Constants.SIZE_VECTOR2);
                }
                CollisionNumSpheres = reader.ReadByte();
            }
            if (Version >= 0x11)
            {
                DrawFlag = reader.ReadByte();
            }
            if (TextureFilter == 0x7)
            {
                DrawFlag = 2;
            }
            if (Version == 0x20)
            {
                reader.ReadBytes(6);
            }
            if (Version > 0x16 && Version < 0x1D && Version != 0x20)
            {
                padAmount = reader.ReadInt32();
                reader.ReadBytes(padAmount * 24);
            }
            else
            {
                if (Version == 0x20)
                {
                    ScaleFactor = reader.ReadSingle();
                    reader.ReadBytes(56);
                }
            }
            ParticleGhostsNum = 0;
            GhostSeparation = 0;
            if (Version >= 0x10)
            {
                if (Version == 0x20)
                {
                    ParticleGhostsNum = reader.ReadInt16();
                    reader.ReadBytes(2);
                }
                else
                {
                    ParticleGhostsNum = (Int16)reader.ReadInt32();
                }
                GhostSeparation = reader.ReadSingle();
            }
            StarRadialPoints = 5;
            StarRadiusRatio = 0.5f;
            if (Version >= 0x19 && Version != 0x20)
            {
                StarRadialPoints = (Int16)reader.ReadInt32();
                StarRadiusRatio = reader.ReadSingle();
            }
            RampTime = 0;
            if (Version >= 0x1A && Version != 0x20)
            {
                RampTime = reader.ReadSingle();
            }
            if (Version != 0x20)
            {
                if (Version > 0x1A)
                {
                    TexturePage = reader.ReadInt32();
                }
                if (Version > 0x1B)
                {
                    ScaleFactor = reader.ReadSingle();
                }
            }
            if (Version >= 0x1E)
            {
                UnkVec3.Read(reader, Constants.SIZE_VECTOR4);
            }
            else
            {
                UnkVec3.X = 10;
                UnkVec3.Y = 10;
                UnkVec3.Z = 10;
                UnkVec3.W = 0;
                if (GenSort == 0)
                {
                    var f1 = MaxSize * 0.0001f;
                    UnkVec3.X = ((Velocity + RandomEmit.X) * ParticleLifeTime + RandomStart.X + f1) * 0.75f;
                    UnkVec3.Y = ((Velocity + RandomEmit.Y) * ParticleLifeTime + RandomStart.Y + f1) * 0.75f;
                    UnkVec3.Z = ((Velocity + RandomEmit.Z) * ParticleLifeTime + RandomStart.Z + f1) * 0.75f;
                }
            }
            var sizePos = reader.BaseStream.Position;
            versionSizeMap.Add(Version, (Int32)(sizePos - basePos));
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Name, 0, 16);
            if (Version == 0x20)
            {
                writer.Write((Byte)0);
                writer.Write(UnkByte1);
            }
            writer.Write(GenRate);
            writer.Write(MaxParticleCount);
            writer.Write(UnkUShort3);
            writer.Write(EmitterOverTime);
            writer.Write(EmitterOverTimeRandom);
            writer.Write(EmitterOffTime);
            writer.Write(EmitterOffTimeRandom);
            writer.Write(GenSort);
            writer.Write(UnkByte3);
            writer.Write(TextureFilter);
            writer.Write(UnkByte5);
            writer.Write(UnkFloat1);
            if (Version >= 0x6)
            {
                writer.Write(CutOnRadius);
                writer.Write(CutOffRadius);
            }
            if (Version >= 0xA)
            {
                writer.Write(DrawCutOff);
            }
            if (!(Version <= 0x16 || Version == 0x20))
            {
                writer.Write(UnkFloat5);
            }
            if (!(Version < 0x18 || Version == 0x20))
            {
                writer.Write(UnkFloat6);
            }
            if (Version < 0x7)
            {
                writer.Write(0);
                writer.Write(0);
            }
            writer.Write(Velocity);
            RandomEmit.Write(writer);
            if (Version < 0x12)
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }
            RandomStart.Write(writer);
            if (Version < 0x12)
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }
            writer.Write(UnkFloat8);
            writer.Write(UnkFloat9);
            writer.Write(UnkFloat10);
            writer.Write(UnkFloat11);
            writer.Write(UnkFloat12);
            writer.Write(UnkFloat13);
            writer.Write(UnkFloat14);
            writer.Write(UnkFloat15);
            writer.Write(UnkFloat16);
            writer.Write(UnkFloat17);
            writer.Write(UnkFloat18);
            writer.Write(UnkFloat19);
            writer.Write(Gravity);
            writer.Write(ParticleLifeTime);
            writer.Write(UnkUShort8);
            writer.Write(UnkByte6);
            writer.Write(UnkByte7);
            writer.Write(UnkFloat22);
            writer.Write(JibberXFreq);
            writer.Write(JibberXAmp);
            writer.Write(JibberYFreq);
            writer.Write(JibberYAmp);
            for (var i = 0; i < 8; ++i)
            {
                ColorGradients[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                AlphaGradient[i].Write(writer);
            }
            if (Version > 0x15)
            {
                Distortion.Write(writer);
            }
            writer.Write(MinSize);
            writer.Write(MaxSize);
            for (var i = 0; i < 8; ++i)
            {
                SizeWidth[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                SizeHeight[i].Write(writer);
            }
            writer.Write(MinRotation);
            writer.Write(MaxRotation);
            for (var i = 0; i < 8; ++i)
            {
                Rotation[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnkGradient1[i].Write(writer);
            }
            for (var i = 0; i < 8; ++i)
            {
                UnkGradient2[i].Write(writer);
            }
            TextureStart.Write(writer);
            TextureEnd.Write(writer);
            if (Version == 0x20)
            {
                writer.Write(0);
            }
            if (Version >= 0x3)
            {
                for (var i = 0; i < 8; ++i)
                {
                    Collision[i].Write(writer);
                }
                writer.Write(CollisionNumSpheres);
            }
            if (Version >= 0x11)
            {
                writer.Write(DrawFlag);
            }
            if (Version == 0x20)
            {
                writer.Write(0);
                writer.Write((Byte)0);
                writer.Write((Byte)0);
            }
            if (Version > 0x16 && Version < 0x1D && Version != 0x20)
            {
                writer.Write(padAmount);
                for (var i = 0; i < padAmount * 6; ++i)
                {
                    writer.Write(0);
                }
            }
            else
            {
                if (Version == 0x20)
                {
                    writer.Write(ScaleFactor);
                    for (var i = 0; i < 14; ++i)
                    {
                        writer.Write(0);
                    }
                }
            }
            if (Version >= 0x10)
            {
                if (Version == 0x20)
                {
                    writer.Write(ParticleGhostsNum);
                    writer.Write((Byte)0);
                    writer.Write((Byte)0);
                }
                else
                {
                    writer.Write((Int32)ParticleGhostsNum);
                }
                writer.Write(GhostSeparation);
            }
            if (Version >= 0x19 && Version != 0x20)
            {
                writer.Write((Int32)StarRadialPoints);
                writer.Write(StarRadiusRatio);
            }
            if (Version >= 0x1A && Version != 0x20)
            {
                writer.Write(RampTime);
            }
            if (Version != 0x20)
            {
                if (Version > 0x1A)
                {
                    writer.Write(TexturePage);
                }
                if (Version > 0x1B)
                {
                    writer.Write(ScaleFactor);
                }
            }
            if (Version >= 0x1E)
            {
                UnkVec3.Write(writer);
            }
        }
    }
}
