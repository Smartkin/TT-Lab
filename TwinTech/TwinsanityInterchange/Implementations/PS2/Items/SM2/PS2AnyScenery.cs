using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2
{
    public class PS2AnyScenery : BaseTwinItem, ITwinScenery
    {
        public Boolean HasLighting { get; set; }
        public String Name { get; set; }
        public UInt32 FogColor { get; set; }
        public Byte UnkByte { get; set; }
        public UInt32 SkydomeID { get; set; }
        public List<AmbientLight> AmbientLights { get; set; }
        public List<DirectionalLight> DirectionalLights { get; set; }
        public List<PointLight> PointLights { get; set; }
        public List<NegativeLight> NegativeLights { get; set; }
        public List<TwinSceneryBaseType> Sceneries { get; set; }

        public PS2AnyScenery()
        {
            AmbientLights = new List<AmbientLight>();
            DirectionalLights = new List<DirectionalLight>();
            PointLights = new List<PointLight>();
            NegativeLights = new List<NegativeLight>();
            Sceneries = new List<TwinSceneryBaseType>();
        }

        public override Int32 GetLength()
        {
            return 4 + 4 + Name.Length + 4 + 4 + 1 +
                (SkydomeID != 0 ? 4 : 0) +
                (HasLighting ? 0x400 + 4 * 5 + AmbientLights.Sum(a => a.GetLength()) +
                    DirectionalLights.Sum(d => d.GetLength()) + PointLights.Sum(p => p.GetLength()) +
                    NegativeLights.Sum(n => n.GetLength()) : 0) +
                Sceneries.Sum(s => s.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            SkydomeID = reader.ReadUInt32();
            var flags = reader.ReadUInt32();
            {
                HasLighting = (flags & 0x20000) != 0;
            }
            var NameLen = reader.ReadInt32();
            Name = new String(reader.ReadChars(NameLen));
            FogColor = reader.ReadUInt32();
            var sceneryType = reader.ReadInt32();
            UnkByte = reader.ReadByte();
            if ((flags & 0x10000) != 0)
            {
                
            }
            if (HasLighting)
            {
                reader.ReadBytes(0x400); // Lights accessor buffer
                reader.ReadInt32(); // Total lights amount
                var ambientLights = reader.ReadInt32();
                var dirLights = reader.ReadInt32();
                var pointLights = reader.ReadInt32();
                var negativeLights = reader.ReadInt32();
                // GetLength methods can be used here since all these classes have static length
                for (var i = 0; i < ambientLights; ++i)
                {
                    var ambient = new AmbientLight();
                    ambient.Read(reader, ambient.GetLength());
                    AmbientLights.Add(ambient);
                }
                for (var i = 0; i < dirLights; ++i)
                {
                    var directional = new DirectionalLight();
                    directional.Read(reader, directional.GetLength());
                    DirectionalLights.Add(directional);
                }
                for (var i = 0; i < pointLights; ++i)
                {
                    var point = new PointLight();
                    point.Read(reader, point.GetLength());
                    PointLights.Add(point);
                }
                for (var i = 0; i < negativeLights; ++i)
                {
                    var negative = new NegativeLight();
                    negative.Read(reader, negative.GetLength());
                    NegativeLights.Add(negative);
                }
            }
            if (sceneryType == 0x160A)
            {
                var root = new TwinSceneryRoot();
                Sceneries.Add(root);
                root.Read(reader, length, Sceneries);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            UInt32 newFlags = 0x0;
            if (SkydomeID != 0)
            {
                newFlags |= 0x10000;
            }
            if (HasLighting)
            {
                newFlags |= 0x20000;
            }
            writer.Write(newFlags);
            writer.Write(Name.Length);
            writer.Write(Name.ToCharArray());
            writer.Write(FogColor);
            writer.Write(Sceneries.Count != 0 ? 0x160A : 3);
            writer.Write(UnkByte);
            if ((newFlags & 0x10000) != 0)
            {
                writer.Write(SkydomeID);
            }
            if (HasLighting)
            {
                for (var i = 0; i < AmbientLights.Count; ++i)
                {
                    writer.Write(i);
                    writer.Write((Int32)LightIdentifier.Ambient);
                }
                for (var i = 0; i < DirectionalLights.Count; ++i)
                {
                    writer.Write(i);
                    writer.Write((Int32)LightIdentifier.Directional);
                }
                for (var i = 0; i < PointLights.Count; i++)
                {
                    writer.Write(i);
                    writer.Write((Int32)LightIdentifier.Point);
                }
                for (var i = 0; i < NegativeLights.Count; ++i)
                {
                    writer.Write(i);
                    writer.Write((Int32)LightIdentifier.Negative);
                }
                var totalLightCount = AmbientLights.Count + DirectionalLights.Count + PointLights.Count + NegativeLights.Count;
                writer.Write(ITwinScenery.GetReservedBlob(), 0, 0x400 - (8 * totalLightCount));
                writer.Write(totalLightCount);
                writer.Write(AmbientLights.Count);
                writer.Write(DirectionalLights.Count);
                writer.Write(PointLights.Count);
                writer.Write(NegativeLights.Count);
                foreach (var l in AmbientLights)
                {
                    l.Write(writer);
                }
                foreach (var l in DirectionalLights)
                {
                    l.Write(writer);
                }
                foreach (var l in PointLights)
                {
                    l.Write(writer);
                }
                foreach (var l in NegativeLights)
                {
                    l.Write(writer);
                }
            }
            if (Sceneries.Count != 0)
            {
                foreach (var s in Sceneries)
                {
                    s.Write(writer);
                }
            }
        }

        public override String GetName()
        {
            return $"Scenery {id:X}";
        }

        private enum LightIdentifier
        {
            Ambient,
            Directional,
            Point,
            Negative
        }
    }
}
