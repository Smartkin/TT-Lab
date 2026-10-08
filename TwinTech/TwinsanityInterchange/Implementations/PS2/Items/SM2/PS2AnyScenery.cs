using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.Libraries;
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
        /// <summary>
        /// Read into the chunk's data and never read again, 0 on every retail chunk but one
        /// </summary>
        public Byte UnusedByte { get; set; }
        public UInt32 SkydomeID { get; set; }
        public List<AmbientLight> AmbientLights { get; set; }
        public List<DirectionalLight> DirectionalLights { get; set; }
        public List<PointLight> PointLights { get; set; }
        public List<SpotLight> SpotLights { get; set; }
        public List<TwinSceneryBaseType> Sceneries { get; set; }
        public List<Int32> LightOrder { get; set; }

        public PS2AnyScenery()
        {
            LightOrder = new List<Int32>();
            AmbientLights = new List<AmbientLight>();
            DirectionalLights = new List<DirectionalLight>();
            PointLights = new List<PointLight>();
            SpotLights = new List<SpotLight>();
            Sceneries = new List<TwinSceneryBaseType>();
        }

        public override Int32 GetLength()
        {
            return 4 + 4 + Name.Length + 4 + 4 + 1 +
                (SkydomeID != 0 ? 4 : 0) +
                (HasLighting ? 0x400 + 4 * 5 + AmbientLights.Sum(a => a.GetLength()) +
                    DirectionalLights.Sum(d => d.GetLength()) + PointLights.Sum(p => p.GetLength()) +
                    SpotLights.Sum(n => n.GetLength()) : 0) +
                Sceneries.Sum(s => s.GetLength());
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var flags = reader.ReadUInt32();
            {
                HasLighting = (flags & 0x20000) != 0;
            }
            var NameLen = reader.ReadInt32();
            Name = GameText.ReadString(reader, NameLen);
            FogColor = reader.ReadUInt32();
            var sceneryType = reader.ReadInt32();
            UnusedByte = reader.ReadByte();
            if ((flags & 0x10000) != 0)
            {
                SkydomeID = reader.ReadUInt32();
            }
            if (HasLighting)
            {
                var accessors = reader.ReadBytes(0x400);
                var totalLights = reader.ReadInt32();
                var ambientLights = reader.ReadInt32();
                var dirLights = reader.ReadInt32();
                var pointLights = reader.ReadInt32();
                var spotLights = reader.ReadInt32();
                LightOrder.Clear();
                for (var i = 0; i < Math.Min(totalLights, 0x80); ++i)
                {
                    LightOrder.Add(BitConverter.ToInt32(accessors, i * 8));
                    LightOrder.Add(BitConverter.ToInt32(accessors, i * 8 + 4));
                }

                if (LightOrder.SequenceEqual(GetDefaultLightOrder(ambientLights, dirLights, pointLights, spotLights)))
                {
                    LightOrder.Clear();
                }
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
                for (var i = 0; i < spotLights; ++i)
                {
                    var spot = new SpotLight();
                    spot.Read(reader, spot.GetLength());
                    SpotLights.Add(spot);
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
            GameText.Write(writer, Name);
            writer.Write(FogColor);
            writer.Write(Sceneries.Count != 0 ? 0x160A : 3);
            writer.Write(UnusedByte);
            if ((newFlags & 0x10000) != 0)
            {
                writer.Write(SkydomeID);
            }
            if (HasLighting)
            {
                var defaultOrder = GetDefaultLightOrder(AmbientLights.Count, DirectionalLights.Count, PointLights.Count, SpotLights.Count);
                // An order that no longer lists every light once goes back to the default one
                var order = LightOrder.Count == defaultOrder.Count && Pairs(LightOrder).OrderBy(pair => pair).SequenceEqual(Pairs(defaultOrder).OrderBy(pair => pair)) ? LightOrder : defaultOrder;
                foreach (var value in order)
                {
                    writer.Write(value);
                }
                var totalLightCount = AmbientLights.Count + DirectionalLights.Count + PointLights.Count + SpotLights.Count;
                writer.Write(ITwinScenery.GetReservedBlob(), 0, 0x400 - (8 * totalLightCount));
                writer.Write(totalLightCount);
                writer.Write(AmbientLights.Count);
                writer.Write(DirectionalLights.Count);
                writer.Write(PointLights.Count);
                writer.Write(SpotLights.Count);
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
                foreach (var l in SpotLights)
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

        private static List<Int32> GetDefaultLightOrder(Int32 ambient, Int32 directional, Int32 point, Int32 spot)
        {
            var order = new List<Int32>();
            var counts = new[] { ambient, directional, point, spot };
            for (var kind = 0; kind < counts.Length; ++kind)
            {
                for (var i = 0; i < counts[kind]; ++i)
                {
                    order.Add(i);
                    order.Add(kind);
                }
            }

            return order;
        }

        private static IEnumerable<(Int32, Int32)> Pairs(List<Int32> order)
        {
            for (var i = 0; i + 1 < order.Count; i += 2)
            {
                yield return (order[i], order[i + 1]);
            }
        }
    }
}
