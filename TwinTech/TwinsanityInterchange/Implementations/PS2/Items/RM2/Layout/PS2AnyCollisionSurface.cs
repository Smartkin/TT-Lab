using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyCollisionSurface : BaseTwinItem, ITwinSurface
    {
        public SurfaceCollisionFlags CollisionMask { get; set; }
        public SurfaceType SurfaceId { get; set; }
        public UInt16 StepSoundId1 { get; set; }
        public UInt16 StepSoundId2 { get; set; }
        public UInt16 WalkOnParticleSystemId { get; set; }
        public UInt16 WalkOnParticleSystemId2 { get; set; }
        public UInt16 LandSoundId1 { get; set; }
        public UInt16 UnkId3 { get; set; }
        public UInt16 LandOnParticleSystemId { get; set; }
        public UInt16 LandSoundId2 { get; set; }
        public UInt16 UnkSoundId { get; set; }
        public Single[] PhysicsParameters { get; set; }
        public Vector4 UnkVec { get; set; }
        public Vector4[] UnkBoundingBox { get; set; }

        public PS2AnyCollisionSurface()
        {
            PhysicsParameters = new float[10];
            UnkVec = new Vector4();
            UnkBoundingBox = new Vector4[2];
        }

        public override int GetLength()
        {
            return 114;
        }

        public override void Read(BinaryReader reader, int length)
        {
            CollisionMask = (SurfaceCollisionFlags)reader.ReadUInt32();
            SurfaceId = (SurfaceType)reader.ReadUInt16();
            StepSoundId1 = reader.ReadUInt16();
            StepSoundId2 = reader.ReadUInt16();
            WalkOnParticleSystemId = reader.ReadUInt16();
            WalkOnParticleSystemId2 = reader.ReadUInt16();
            LandSoundId1 = reader.ReadUInt16();
            UnkId3 = reader.ReadUInt16();
            LandOnParticleSystemId = reader.ReadUInt16();
            LandSoundId2 = reader.ReadUInt16();
            UnkSoundId = reader.ReadUInt16();
            reader.ReadUInt16(); // Unused ID
            for (int i = 0; i < PhysicsParameters.Length; ++i)
            {
                PhysicsParameters[i] = reader.ReadSingle();
            }
            UnkVec.Read(reader, Constants.SIZE_VECTOR4);
            for (int i = 0; i < UnkBoundingBox.Length; ++i)
            {
                UnkBoundingBox[i] = new Vector4();
                UnkBoundingBox[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)CollisionMask);
            writer.Write((UInt16)SurfaceId);
            writer.Write(StepSoundId1);
            writer.Write(StepSoundId2);
            writer.Write(WalkOnParticleSystemId);
            writer.Write(WalkOnParticleSystemId2);
            writer.Write(LandSoundId1);
            writer.Write(UnkId3);
            writer.Write(LandOnParticleSystemId);
            writer.Write(LandSoundId2);
            writer.Write(UnkSoundId);
            writer.Write((UInt16)0xFFFF);
            for (int i = 0; i < PhysicsParameters.Length; ++i)
            {
                writer.Write(PhysicsParameters[i]);
            }
            UnkVec.Write(writer);
            for (int i = 0; i < UnkBoundingBox.Length; ++i)
            {
                UnkBoundingBox[i].Write(writer);
            }
        }

        public override String GetName()
        {
            return $"{SurfaceId}_{id:X}";
        }
    }
}
