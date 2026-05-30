using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnyMaterial : BaseTwinItem, ITwinMaterial
    {
        public AppliedShaders ActivatedShaders { get; set; }
        public UInt32 DmaChainIndex { get; set; }
        public String Name { get; set; }
        public List<TwinShader> Shaders { get; set; }
        public PS2AnyMaterial()
        {
            Shaders = new List<TwinShader>();
        }

        public override int GetLength()
        {
            return 20 + (Name.Length + 1) + Shaders.Sum(s => s.GetLength());
        }

        public override void Read(BinaryReader reader, int length)
        {
            ActivatedShaders = (AppliedShaders)reader.ReadUInt64();
            DmaChainIndex = reader.ReadUInt32();
            Int32 NameLen = reader.ReadInt32();
            Name = new string(reader.ReadChars(NameLen - 1));
            reader.ReadChar();
            Int32 shaderCount = reader.ReadInt32();
            Shaders.Clear();
            for (int i = 0; i < shaderCount; ++i)
            {
                TwinShader shader = new();
                shader.Read(reader, 0);
                Shaders.Add(shader);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((UInt64)ActivatedShaders);
            writer.Write(DmaChainIndex);
            writer.Write(Name.Length + 1);
            writer.Write(Name.ToCharArray());
            writer.Write('\0');
            writer.Write(Shaders.Count);
            foreach (ITwinSerializable shader in Shaders)
            {
                shader.Write(writer);
            }
        }

        public override String GetName()
        {
            return $"{Name}_{id:X}";
        }
    }
}
