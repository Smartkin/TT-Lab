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

        // A sound's samples are where its header says, which is where the game reads them. TT Lab's builds lay them out in the order the
        // sounds were put in and the code section sorts the records by their IDs after that: adding the lengths up in the records' order took
        // other sounds' bytes for most sounds of a TT Lab build. A stereo sound's right half comes after every sound's (left) samples, in the
        // order of their offsets
        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            var sounds = Items.Cast<PS2AnySound>().ToList();
            if (sounds.Any(sound => sound.offset < 0 || (Int64)sound.offset + ChannelLength(sound) > extraData.Length))
            {
                // Offsets that don't fit the samples: one after the other in the records' order
                var next = 0;
                foreach (var sound in sounds)
                {
                    sound.offset = next;
                    next += ChannelLength(sound);
                }
            }

            var end = 0;
            foreach (var sound in sounds)
            {
                Array.Copy(extraData, sound.offset, sound.Sound, 0, ChannelLength(sound));
                end = Math.Max(end, sound.offset + ChannelLength(sound));
            }

            foreach (var sound in sounds.Where(sound => (sound.Header & 1) == 0).OrderBy(sound => sound.offset))
            {
                Array.Copy(extraData, end, sound.Sound, sound.Sound.Length / 2, sound.Sound.Length / 2);
                end += sound.Sound.Length / 2;
            }
        }

        // The bytes of a mono sound's samples, of a stereo sound's left half
        private static Int32 ChannelLength(PS2AnySound sound)
        {
            return (sound.Header & 1) == 0 ? sound.Sound.Length / 2 : sound.Sound.Length;
        }

        protected override void PreprocessWrite()
        {
            base.PreprocessWrite();
            using var newExtraData = new MemoryStream();
            var offset = 0;
            foreach (PS2AnySound item in Items.Cast<PS2AnySound>())
            {
                var writeLength = ChannelLength(item);
                newExtraData.Write(item.Sound, 0, writeLength);
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
