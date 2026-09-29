using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common.Particles;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2
{
    public class PS2DefaultParticleData : PS2AnyParticleData, ITwinDefaultParticle
    {
        public UInt32[] TextureIDs { get; set; }
        public UInt32[] MaterialIDs { get; set; }
        public UInt32 DecalTextureID { get; set; }
        public UInt32 DecalMaterialID { get; set; }
        public Int32 UnusedDecalInt { get; set; }
        public TwinDecalUvPacket DecalUvPacket { get; set; }
        public Int32[] DecalTypeMarkers { get; set; }
        public List<TwinDecalType> DecalTypes { get; set; }

        public PS2DefaultParticleData() : base()
        {
            DecalTypes = new List<TwinDecalType>();
            TextureIDs = new UInt32[3];
            MaterialIDs = new UInt32[3];
            DecalTypeMarkers = new Int32[16];
            DecalUvPacket = new TwinDecalUvPacket();
        }

        public override Int32 GetLength()
        {
            return 24 + 8 + ParticleSystems.Sum(t => t.GetLength()) + 12 + TwinDecalUvPacket.Length + 0x40 + DecalTypes.Sum(b => b.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            for (var i = 0; i < 3; ++i)
            {
                TextureIDs[i] = reader.ReadUInt32();
                MaterialIDs[i] = reader.ReadUInt32();
            }
            Version = reader.ReadUInt32();
            if ((Version < 0x5 || Version > 0x1E) && Version != 0x20)
            {
                throw new Exception($"Invalid/Deprecated particle data section version: {Version}");
            }
            var systemsAmount = reader.ReadInt32();
            for (var i = 0; i < systemsAmount; ++i)
            {
                var type = new TwinParticleSystem(Version);
                type.Read(reader, length);
                ParticleSystems.Add(type);
            }
            DecalTextureID = reader.ReadUInt32();
            DecalMaterialID = reader.ReadUInt32();
            UnusedDecalInt = reader.ReadInt32();
            DecalUvPacket.Read(reader, TwinDecalUvPacket.Length);
            for (var i = 0; i < 16; ++i)
            {
                DecalTypeMarkers[i] = reader.ReadInt32();
            }
            DecalTypes.Clear();
            for (var i = 0; i < 16; ++i)
            {
                if (DecalTypeMarkers[i] != 0)
                {
                    var type = new TwinDecalType();
                    type.Read(reader, TwinDecalType.Length);
                    DecalTypes.Add(type);
                }
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (var i = 0; i < 3; ++i)
            {
                writer.Write(TextureIDs[i]);
                writer.Write(MaterialIDs[i]);
            }
            // We can do base here because we gonna have 0 particle emitters, so this is fine
            base.Write(writer);
            // Move back because base writes additional particle emitters amount
            writer.BaseStream.Position -= 4;
            writer.Write(DecalTextureID);
            writer.Write(DecalMaterialID);
            writer.Write(UnusedDecalInt);
            DecalUvPacket.Write(writer);
            for (var i = 0; i < 16; ++i)
            {
                writer.Write(DecalTypeMarkers[i]);
            }
            foreach (var type in DecalTypes)
            {
                type.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"Default particle data {id:X}";
        }
    }
}
