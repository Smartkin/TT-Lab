using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox
{
    public class XboxPSF : BaseTwinItem, ITwinPSF
    {
        public List<ITwinPTC> FontPages { get; set; }
        public List<VectorCharacterData> CharacterData { get; set; }
        public Int32 SpaceIdentifier { get; set; }

        public override Int32 GetLength()
        {
            return 4 + FontPages.Sum(f => f.GetLength()) + CharacterData.Count * Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var pages = reader.ReadInt32();
            for (var i = 0; i < pages; ++i)
            {
                var page = new XboxPTC();
                page.Read(reader, 0);
                FontPages.Add(page);
            }
            var vecAmt = reader.ReadInt32();
            SpaceIdentifier = reader.ReadInt32();
            for (var i = 0; i < vecAmt; ++i)
            {
                var vec = new VectorCharacterData();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                CharacterData.Add(vec);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(FontPages.Count);
            foreach (var page in FontPages)
            {
                page.Write(writer);
            }
            writer.Write(CharacterData.Count);
            writer.Write(SpaceIdentifier);
            foreach (var v in CharacterData)
            {
                v.Write(writer);
            }
        }
    }
}
