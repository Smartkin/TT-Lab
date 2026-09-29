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
        public UInt16 ImpactParticleSystemId { get; set; }
        public UInt16 HardImpactParticleSystemId { get; set; }
        public UInt16 ImpactSoundId { get; set; }
        public UInt16 HardImpactSoundId { get; set; }
        public UInt16 StepParticleSystemId { get; set; }
        public UInt16 LandSoundId { get; set; }
        public UInt16 ScrapeSoundId { get; set; }
        public Single[] PhysicsParameters { get; set; }
        public Vector4 UnusedVector { get; set; }
        public Vector4[] ContactMessage { get; set; }

        public PS2AnyCollisionSurface()
        {
            PhysicsParameters = new float[SurfacePhysics.Count];
            UnusedVector = new Vector4();
            ContactMessage = new Vector4[2];
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
            ImpactParticleSystemId = reader.ReadUInt16();
            HardImpactParticleSystemId = reader.ReadUInt16();
            ImpactSoundId = reader.ReadUInt16();
            HardImpactSoundId = reader.ReadUInt16();
            StepParticleSystemId = reader.ReadUInt16();
            LandSoundId = reader.ReadUInt16();
            ScrapeSoundId = reader.ReadUInt16();
            reader.ReadUInt16(); // Unused ID
            for (int i = 0; i < PhysicsParameters.Length; ++i)
            {
                PhysicsParameters[i] = reader.ReadSingle();
            }
            UnusedVector.Read(reader, Constants.SIZE_VECTOR4);
            for (int i = 0; i < ContactMessage.Length; ++i)
            {
                ContactMessage[i] = new Vector4();
                ContactMessage[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)CollisionMask);
            writer.Write((UInt16)SurfaceId);
            writer.Write(StepSoundId1);
            writer.Write(StepSoundId2);
            writer.Write(ImpactParticleSystemId);
            writer.Write(HardImpactParticleSystemId);
            writer.Write(ImpactSoundId);
            writer.Write(HardImpactSoundId);
            writer.Write(StepParticleSystemId);
            writer.Write(LandSoundId);
            writer.Write(ScrapeSoundId);
            writer.Write((UInt16)0xFFFF);
            for (int i = 0; i < PhysicsParameters.Length; ++i)
            {
                writer.Write(PhysicsParameters[i]);
            }
            UnusedVector.Write(writer);
            for (int i = 0; i < ContactMessage.Length; ++i)
            {
                ContactMessage[i].Write(writer);
            }
        }

        public override String GetName()
        {
            return $"{SurfaceId}_{id:X}";
        }
    }
}
