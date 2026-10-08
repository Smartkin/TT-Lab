using System;
using System.IO;
using Twinsanity.Libraries;
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
            Path = GameText.ReadString(reader, chars);
            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Path.Length);
            Path = Path.Replace('/', '\\');
            GameText.Write(writer, Path);
            writer.Write(Offset);
            writer.Write(Length);
        }
    }
}
