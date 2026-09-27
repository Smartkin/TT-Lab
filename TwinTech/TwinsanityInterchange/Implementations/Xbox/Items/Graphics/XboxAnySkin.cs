using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics
{
    public class XboxAnySkin : BaseTwinItem, ITwinSkin
    {
        public List<ITwinSubSkin> SubSkins { get; set; }

        public XboxAnySkin()
        {
            SubSkins = new();
        }

        public override Int32 GetLength()
        {
            return 4 + SubSkins.Sum((ss) => { return ss.GetLength(); });
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            var subSkins = reader.ReadInt32();
            for (int i = 0; i < subSkins; ++i)
            {
                var subSkin = new XboxSubSkin();
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

        public override String GetName()
        {
            return $"Skin {id:X}";
        }
    }
}
