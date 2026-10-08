using System;
using System.IO;

namespace Twinsanity.Libraries
{
    /// <summary>
    /// The game's names and paths are a byte per character. They were read and written in the reader's and writer's encoding (UTF-8),
    /// which made a name with a character past ASCII longer than its length says: the rest of its item was read from the wrong place
    /// </summary>
    public static class GameText
    {
        /// <summary>
        /// Reads that many characters, a byte each
        /// </summary>
        public static Char[] ReadChars(BinaryReader reader, Int32 count)
        {
            var bytes = reader.ReadBytes(count);
            var chars = new Char[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i] = (Char)bytes[i];
            }

            return chars;
        }

        /// <summary>
        /// Reads a text of that many characters, a byte each
        /// </summary>
        public static String ReadString(BinaryReader reader, Int32 count)
        {
            return new String(ReadChars(reader, count));
        }

        /// <summary>
        /// Writes the characters a byte each, the ones no byte holds as '?'
        /// </summary>
        public static void Write(BinaryWriter writer, Char[] chars, Int32 index, Int32 count)
        {
            var bytes = new Byte[count];
            for (var i = 0; i < count; i++)
            {
                var character = chars[index + i];
                bytes[i] = character > 0xFF ? (Byte)'?' : (Byte)character;
            }

            writer.Write(bytes);
        }

        /// <summary>
        /// Writes the text a byte per character, the ones no byte holds as '?'
        /// </summary>
        public static void Write(BinaryWriter writer, String text)
        {
            Write(writer, text.ToCharArray(), 0, text.Length);
        }
    }
}
