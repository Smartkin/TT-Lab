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
        reader.BaseStream.Position = 22;
        channels = reader.ReadInt16();
        samplerate = reader.ReadUInt32();
        reader.BaseStream.Position += 12;
        var len = reader.ReadInt32();
        pcm = reader.ReadBytes(len);
        reader.BaseStream.Position = 0;
        return reader.ReadBytes((int)reader.BaseStream.Length);
    }
}