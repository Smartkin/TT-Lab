using System;
using System.IO;

namespace Twinsanity.Libraries;

/// <summary>
/// Helper class to save/load WAV sound files
/// </summary>
public static class Riff
{
    /// <summary>
    /// Saves the sound data in WAV format
    /// </summary>
    /// <param name="writer">Stream to save into</param>
    /// <param name="pcm">Sound data</param>
    /// <param name="channels">Amount of audio channels</param>
    /// <param name="samplerate"></param>
    public static void SaveRiff(BinaryWriter writer, byte[] pcm, ref short channels, ref UInt32 samplerate)
    {
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write(channels);
        writer.Write(samplerate);
        writer.Write((uint)(samplerate * channels * 2));
        writer.Write((short)(channels * 2));
        writer.Write((ushort)16);
        writer.Write("data".ToCharArray());
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }
    
    /// <summary>
    /// Reads the provided WAV stores the data in provided data
    /// </summary>
    /// <param name="reader">Stream to read from</param>
    /// <param name="pcm">Sound data to store</param>
    /// <param name="channels">Amount of channels</param>
    /// <param name="samplerate"></param>
    /// <returns>The file buffer is returned as a byte array</returns>
    public static byte[] LoadRiff(BinaryReader reader, ref byte[] pcm, ref short channels, ref UInt32 samplerate)
    {
        return LoadRiff(reader, ref pcm, ref channels, ref samplerate, out _, out _);
    }

    /// <summary>
    /// Reads the provided WAV like <see cref="LoadRiff(BinaryReader, ref byte[], ref short, ref uint)"/>, and the first loop of its
    /// sampler chunk ("smpl", written by audio tools): its first sample and the one after its last, -1 for both without one. The chunks
    /// are read by their headers, so files with other chunks (LIST, fact, a longer fmt) read the same
    /// </summary>
    public static byte[] LoadRiff(BinaryReader reader, ref byte[] pcm, ref short channels, ref UInt32 samplerate, out Int32 loopStart, out Int32 loopEnd)
    {
        loopStart = -1;
        loopEnd = -1;
        var stream = reader.BaseStream;
        stream.Position = 12;
        var foundData = false;
        while (stream.Position + 8 <= stream.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadUInt32();
            var start = stream.Position;
            switch (id)
            {
                case "fmt ":
                    reader.ReadUInt16();
                    channels = reader.ReadInt16();
                    samplerate = reader.ReadUInt32();
                    break;
                case "data":
                    pcm = reader.ReadBytes((int)Math.Min(size, stream.Length - start));
                    foundData = true;
                    break;
                case "smpl" when size >= 36:
                    stream.Position = start + 28;
                    var loops = reader.ReadUInt32();
                    if (loops > 0 && size >= 60)
                    {
                        stream.Position = start + 36 + 8;
                        loopStart = reader.ReadInt32();
                        loopEnd = reader.ReadInt32() + 1;
                    }

                    break;
            }

            // Chunks are padded to an even size
            stream.Position = start + size + (size & 1);
        }

        if (!foundData)
        {
            throw new InvalidDataException("The WAV file has no data chunk");
        }

        stream.Position = 0;
        return reader.ReadBytes((int)stream.Length);
    }
}