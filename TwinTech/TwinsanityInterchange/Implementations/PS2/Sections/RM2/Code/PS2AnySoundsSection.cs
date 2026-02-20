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
                var copyLength = item.Sound.Length;
                if ((item.Header & 1) == 0)
                {
                    copyLength /= 2;
                }
                
                Array.Copy(extraData, offset, item.Sound, 0, copyLength);
                offset += copyLength;
            }
            
            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                if ((item.Header & 1) != 0)
                {
                    continue;
                }
                
                Array.Copy(extraData, offset, item.Sound, item.Sound.Length / 2, item.Sound.Length / 2);
                offset += item.Sound.Length / 2;
            }
        }

        protected override void PreprocessWrite()
        {
            base.PreprocessWrite();
            using var newExtraData = new MemoryStream();
            var offset = 0;
            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                var writeLength = item.Sound.Length;
                if ((item.Header & 1) == 0)
                {
                    writeLength /= 2;
                    newExtraData.Write(item.Sound, 0, writeLength);
                }
                else
                {
                    newExtraData.Write(item.Sound);
                }

                item.offset = offset;
                offset += writeLength;
            }

            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                if ((item.Header & 1) != 0)
                {
                    continue;
                }
                
                newExtraData.Write(item.Sound, item.Sound.Length / 2, item.Sound.Length / 2);
            }
            newExtraData.Flush();
            extraData = newExtraData.ToArray();
        }
    }
}
