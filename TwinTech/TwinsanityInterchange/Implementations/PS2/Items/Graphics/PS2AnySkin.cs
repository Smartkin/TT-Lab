using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics
{
    public class PS2AnySkin : BaseTwinItem, ITwinSkin
    {
        public List<ITwinSubSkin> SubSkins { get; set; }

        public PS2AnySkin()
        {
            SubSkins = new List<ITwinSubSkin>();
        }

        public override int GetLength()
        {
            return 4 + SubSkins.Sum((ss) => { return ss.GetLength(); });
        }

        public override void Read(BinaryReader reader, int length)
        {
            var subSkins = reader.ReadInt32();
            for (int i = 0; i < subSkins; ++i)
            {
                var subSkin = new PS2SubSkin();
                subSkin.Read(reader, length);
                SubSkins.Add(subSkin);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(SubSkins.Count);
            foreach (ITwinSerializable subSkin in SubSkins)
            {
                subSkin.Write(writer);
            }
        }

        public override void Compile()
        {
            base.Compile();
            foreach (var subSkin in SubSkins)
            {
                subSkin.Compile();
            }
        }

        public override String GetName()
        {
            return $"Skin {id:X}";
        }
    }
}
