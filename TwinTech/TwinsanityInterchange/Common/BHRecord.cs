using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class BHRecord : ITwinSerializable
    {
        public String Path;
        public Int32 Offset;
        public Int32 Length;

        public Int32 GetLength()
        {
            return 12 + Path.Length;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            var chars = reader.ReadInt32();
            Path = new String(reader.ReadChars(chars));
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Path.Length);
            Path = Path.Replace('/', '\\');
            writer.Write(Path.ToCharArray());
            writer.Write(Offset);
            writer.Write(Length);
        }
    }
}
