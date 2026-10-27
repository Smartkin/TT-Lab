using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives
{
    public class PS2BD : ITwinSerializable
    {
        private const Int32 CopyBufferSize = 0x100000;

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

        // Where the archive keeps each of its files, read from its header without reading any of the files
        public static List<BHRecord> ReadRecords(String headerPath)
        {
            var header = new PS2BH();
            using var stream = new FileStream(headerPath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);
            header.Read(reader, (Int32)stream.Length);
            return header.Records;
        }

        public static Byte[] ReadFile(String archivePath, BHRecord record)
        {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Position = record.Offset;
            var data = new Byte[record.Length];
            stream.ReadExactly(data);
            return data;
        }

        public void BuildRecords(string folderSource)
        {
            var offset = 0;
            foreach (var (file, path) in ArchiveFiles(folderSource))
            {
                using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
                using BinaryReader br = new(fs);
                var head = RecordOf(file, path, offset, fs.Length);
                Header.Records.Add(head);
                var last = new BDRecord(head, br.ReadBytes(head.Length));
                offset += head.Length;
                Items.Add(last);
            }
        }

        /// <summary>
        /// Writes an archive of the folder's files: the files into <paramref name="bd"/> one at a time through a buffer, and the header into
        /// <paramref name="bh"/> once every file is in. The bytes are the ones <see cref="BuildRecords"/> and <see cref="Write"/> make,
        /// without holding the files in memory. The streams are left open
        /// </summary>
        /// <exception cref="IOException">The archive would go past <see cref="Int32.MaxValue"/> bytes, which its header's offsets and lengths
        /// can't hold. The message names the file that would take it there, and nothing goes into <paramref name="bh"/></exception>
        public static void WriteArchive(String archivesFolder, Stream bh, Stream bd)
        {
            ArgumentNullException.ThrowIfNull(bh);
            ArgumentNullException.ThrowIfNull(bd);

            var header = new PS2BH();
            var buffer = new Byte[CopyBufferSize];
            var offset = 0;
            foreach (var (file, path) in ArchiveFiles(archivesFolder))
            {
                using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
                var record = RecordOf(file, path, offset, source.Length);
                CopyExactly(source, bd, record.Length, buffer, file);
                header.Records.Add(record);
                offset += record.Length;
            }

            bd.Flush();
            using var writer = new BinaryWriter(bh, Encoding.UTF8, true);
            header.Write(writer);
            writer.Flush();
        }

        // The folder's files in the order the archive keeps them, each with its path in the archive: the folder and a separator taken out
        // wherever they are, the way the archive's paths were always made
        private static IEnumerable<(String File, String ArchivePath)> ArchiveFiles(String folder)
        {
            var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            if (!folder.EndsWith(Path.DirectorySeparatorChar))
            {
                folder += Path.DirectorySeparatorChar;
            }

            return files.Select(file => (file, file.Replace(folder, "")));
        }

        // The length is the open file's: FileInfo's is a symbolic link's own. The header keeps offsets and lengths as Int32s, which an archive
        // past Int32.MaxValue bytes wrapped around
        private static BHRecord RecordOf(String file, String archivePath, Int32 offset, Int64 length)
        {
            if (length > Int32.MaxValue - offset)
            {
                throw new IOException($"{file} doesn't fit into the archive: it would end {offset + length} bytes in, past the {Int32.MaxValue} its header can hold");
            }

            return new BHRecord
            {
                Length = (Int32)length,
                Offset = offset,
                Path = archivePath
            };
        }

        // Exactly the record's length, so the archive's data stays where its header says even when a file changes while it's copied
        private static void CopyExactly(Stream source, Stream destination, Int32 length, Byte[] buffer, String file)
        {
            while (length > 0)
            {
                var read = source.Read(buffer, 0, Math.Min(buffer.Length, length));
                if (read == 0)
                {
                    throw new EndOfStreamException($"{file} got shorter while it was copied into the archive, {length} of its bytes were missing");
                }

                destination.Write(buffer, 0, read);
                length -= read;
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
