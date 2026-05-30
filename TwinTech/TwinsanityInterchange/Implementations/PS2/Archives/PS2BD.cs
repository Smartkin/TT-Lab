using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    public class PS2BD : ITwinSerializable
    {
        private PS2BH Header;
        private String headerPath;
        private String headerWritePath;
        public List<BDRecord> Items;

        // A requirement to provide the header path
        public PS2BD(String headerPath, String headerWritePath)
        {
            this.headerWritePath = headerWritePath;
            this.headerPath = headerPath;
            Header = new PS2BH();
            Items = new List<BDRecord>();
        }

        public void BuildRecords(string folderSource)
        {
            var files = Directory.GetFiles(folderSource, "*.*", SearchOption.AllDirectories);
            var offset = 0;
            if (!folderSource.EndsWith(Path.DirectorySeparatorChar))
            {
                folderSource += Path.DirectorySeparatorChar;
            }
            
            foreach (var file in files)
            {
                var head = new BHRecord
                {
                    Length = (int)(new FileInfo(file)).Length,
                    Offset = offset,
                    Path = file.Replace(folderSource, "")
                };
                Header.Records.Add(head);
                using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
                using BinaryReader br = new(fs);
                var last = new BDRecord(head, br.ReadBytes(head.Length));
                offset += head.Length;
                Items.Add(last);
            }
        }

        public Int32 GetLength()
        {
            return Items.Sum(i => i.Data.Length);
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            using FileStream headerStream = new(headerPath, FileMode.Open, FileAccess.Read);
            using BinaryReader headerReader = new(headerStream);
            Header.Read(headerReader, (Int32)headerStream.Length);

            foreach (var record in Header.Records)
            {
                reader.BaseStream.Position = record.Offset;
                var r = new BDRecord(record, reader.ReadBytes(record.Length));
                Items.Add(r);
            }
        }

        public void Write(BinaryWriter writer)
        {
            using FileStream stream = new(headerWritePath, FileMode.Create, FileAccess.Write);
            using BinaryWriter headerWriter = new(stream);
            Header.Write(headerWriter);

            foreach (var item in Items)
            {
                writer.Write(item.Data);
            }
        }
    }
}
