using System;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code
{
    public class PS2AnySoundsSection : BaseTwinSection
    {
        public PS2AnySoundsSection() : base()
        {
            defaultType = typeof(PS2AnySound);
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            var offset = 0;
            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                Array.Copy(extraData, offset, item.Sound, 0, item.Sound.Length);
                offset += item.Sound.Length;
            }
        }

        protected override void PreprocessWrite()
        {
            base.PreprocessWrite();
            using var newExtraData = new MemoryStream();
            var offset = 0;
            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                newExtraData.Write(item.Sound);
                item.offset = offset;
                offset += item.Sound.Length;
            }
            newExtraData.Flush();
            extraData = newExtraData.ToArray();
        }
    }
}
