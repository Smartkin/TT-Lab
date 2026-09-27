using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.PS2Hardware;

/// <summary>
/// How a VIF packet got padded to whole quad words
/// </summary>
public enum TwinVifPadding
{
    /// <summary>
    /// NOPs up to the next quad word, how the PAL release was built
    /// </summary>
    QuadWord,
    /// <summary>
    /// A NOP for every byte of padding, how the NTSC release was built. The DMA tag only covers the NOPs up to the quad word
    /// </summary>
    NopPerByte
}

/// <summary>
/// One UNPACK command of a VIF packet together with the VIF state it gets unpacked with
/// </summary>
public class TwinVifUnpack
{
    /// <summary>
    /// VU memory address the data gets unpacked to
    /// </summary>
    public UInt16 Address { get; set; }
    /// <summary>
    /// Format of the packed data
    /// </summary>
    public PackFormat Format { get; set; }
    /// <summary>
    /// Whether the packed components get zero extended instead of sign extended
    /// </summary>
    public Boolean IsUnsigned { get; set; }
    /// <summary>
    /// Amount of vectors unpacked
    /// </summary>
    public Int32 Amount { get; set; }
    /// <summary>
    /// The packed data words
    /// </summary>
    public UInt32[] Data { get; set; }
    /// <summary>
    /// Whether the row registers get added to the unpacked components
    /// </summary>
    public Boolean IsOffsetMode { get; set; }
    /// <summary>
    /// The row registers at the time of unpacking
    /// </summary>
    public UInt32[] Row { get; set; }

    /// <summary>
    /// Unpacks the components of the vector with the given index the way VIF writes them into VU memory
    /// </summary>
    public UInt32[] GetVector(Int32 index)
    {
        var result = new UInt32[4];
        switch (Format)
        {
            case PackFormat.V2_32:
                result[0] = Data[index * 2];
                result[1] = Data[index * 2 + 1];
                break;
            case PackFormat.V3_32:
                result[0] = Data[index * 3];
                result[1] = Data[index * 3 + 1];
                result[2] = Data[index * 3 + 2];
                break;
            case PackFormat.V4_32:
                result[0] = Data[index * 4];
                result[1] = Data[index * 4 + 1];
                result[2] = Data[index * 4 + 2];
                result[3] = Data[index * 4 + 3];
                break;
            case PackFormat.V4_16:
                result[0] = Extend(Data[index * 2] & 0xFFFF, 16);
                result[1] = Extend(Data[index * 2] >> 16, 16);
                result[2] = Extend(Data[index * 2 + 1] & 0xFFFF, 16);
                result[3] = Extend(Data[index * 2 + 1] >> 16, 16);
                break;
            case PackFormat.V4_8:
                result[0] = Extend(Data[index] & 0xFF, 8);
                result[1] = Extend(Data[index] >> 8 & 0xFF, 8);
                result[2] = Extend(Data[index] >> 16 & 0xFF, 8);
                result[3] = Extend(Data[index] >> 24 & 0xFF, 8);
                break;
            default:
                throw new NotSupportedException($"Unpacking {Format} vectors is not supported");
        }

        if (!IsOffsetMode)
        {
            return result;
        }

        for (var i = 0; i < 4; i++)
        {
            result[i] += Row[i];
        }

        return result;
    }

    private UInt32 Extend(UInt32 value, Int32 bits)
    {
        if (IsUnsigned)
        {
            return value;
        }

        var signBit = 1U << (bits - 1);
        return (value & signBit) != 0 ? value | ~((signBit << 1) - 1) : value;
    }
}

/// <summary>
/// Reads and writes the VIF packets Twinsanity stores its models in: a DMA tag followed by batches of UNPACK commands that each end
/// with an MSCAL starting the VU program on them
/// </summary>
public static class TwinVifPacket
{
    // MARK 0xFA and a NOP, the DMA tag's second half gets sent to VIF as codes
    private const UInt64 DmaTagExtra = 0x00000000070000FA;

    /// <summary>
    /// Splits a packet into its batches of unpacks. A batch starts at an unpack to VU address 0, which is where the GIF tag goes
    /// </summary>
    public static List<List<TwinVifUnpack>> ReadBatches(Byte[] packet, Boolean hasDmaTag = true)
    {
        var batches = new List<List<TwinVifUnpack>>();
        var row = new UInt32[4];
        var offsetMode = false;
        using var stream = new MemoryStream(packet);
        using var reader = new BinaryReader(stream);
        var end = stream.Length;
        if (hasDmaTag)
        {
            var dmaTag = reader.ReadUInt64();
            end = Math.Min(end, 16 + (Int64)(dmaTag & 0xFFFF) * 16);
        }

        while (stream.Position + 4 <= end)
        {
            var code = new VIFCode();
            code.Read(reader);
            if (code.IsUnpack())
            {
                var command = (Byte)code.OP;
                var format = (PackFormat)(command & 0xF);
                var amount = code.Amount == 0 ? 256 : code.Amount;
                var bits = (32 >> (command & 0x3)) * ((command >> 2 & 0x3) + 1) * amount;
                var words = (bits + 31) / 32;
                var data = new UInt32[words];
                for (var i = 0; i < words; i++)
                {
                    data[i] = reader.ReadUInt32();
                }

                var unpack = new TwinVifUnpack
                {
                    Address = (UInt16)(code.Immediate & 0x3FF),
                    Format = format,
                    IsUnsigned = (code.Immediate & 0x4000) != 0,
                    Amount = amount,
                    Data = data,
                    IsOffsetMode = offsetMode,
                    Row = (UInt32[])row.Clone()
                };
                if (unpack.Address == 0 || batches.Count == 0)
                {
                    batches.Add(new List<TwinVifUnpack>());
                }

                batches[^1].Add(unpack);
                continue;
            }

            switch (code.OP)
            {
                case VIFCodeEnum.STMOD:
                    offsetMode = (code.Immediate & 0x3) == 1;
                    break;
                case VIFCodeEnum.STROW:
                    for (var i = 0; i < 4; i++)
                    {
                        row[i] = reader.ReadUInt32();
                    }
                    break;
                case VIFCodeEnum.STCOL:
                    reader.ReadBytes(16);
                    break;
                case VIFCodeEnum.STMASK:
                    reader.ReadUInt32();
                    break;
            }
        }

        return batches;
    }

    /// <summary>
    /// Finds out how a packet was padded from the NOPs after its last code
    /// </summary>
    public static TwinVifPadding DetectPadding(Byte[] packet)
    {
        var nops = 0;
        for (var end = packet.Length - 4; end >= 16 && BitConverter.ToUInt32(packet, end) == 0; end -= 4)
        {
            nops++;
        }

        var length = packet.Length - nops * 4;
        return length % 16 != 0 && nops == 16 - length % 16 ? TwinVifPadding.NopPerByte : TwinVifPadding.QuadWord;
    }

    /// <summary>
    /// Builds a packet the same way Twinsanity's tools did
    /// </summary>
    public sealed class Writer
    {
        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _writer;

        /// <summary>
        /// Starts a packet with its DMA tag
        /// </summary>
        public Writer(Boolean hasDmaTag = true)
        {
            _writer = new BinaryWriter(_stream);
            if (hasDmaTag)
            {
                _writer.Write(0UL);
                _writer.Write(DmaTagExtra);
            }
        }

        /// <summary>
        /// Writes a VIF code without data
        /// </summary>
        public void Code(VIFCodeEnum op, UInt16 immediate = 0)
        {
            new VIFCode { OP = op, Immediate = immediate }.Write(_writer);
        }

        /// <summary>
        /// Writes a code followed by the 4 words of the row or column registers
        /// </summary>
        public void Registers(VIFCodeEnum op, UInt32[] values)
        {
            Code(op);
            for (var i = 0; i < 4; i++)
            {
                _writer.Write(values[i]);
            }
        }

        /// <summary>
        /// Writes an UNPACK of already packed data words
        /// </summary>
        public void Unpack(UInt16 address, PackFormat format, Int32 amount, IEnumerable<UInt32> data, Boolean isUnsigned = false)
        {
            var immediate = (UInt16)(address | 0x8000 | (isUnsigned ? 0x4000 : 0));
            var code = new VIFCode { OP = (VIFCodeEnum)((Byte)VIFCodeEnum.UNPACK | (Byte)format), Amount = (Byte)amount, Immediate = immediate };
            code.Write(_writer);
            foreach (var word in data)
            {
                _writer.Write(word);
            }
        }

        /// <summary>
        /// Pads the packet and fills in the DMA tag
        /// </summary>
        public Byte[] Finish(TwinVifPadding paddingStyle, Boolean hasDmaTag = true)
        {
            _writer.Flush();
            var length = (Int32)_stream.Length;
            var padding = (16 - length % 16) % 16;
            if (paddingStyle == TwinVifPadding.QuadWord)
            {
                padding /= 4;
            }

            for (var i = 0; i < padding; i++)
            {
                _writer.Write(0U);
            }

            _writer.Flush();
            var result = _stream.ToArray();
            if (hasDmaTag)
            {
                var quadWords = (UInt64)((length + 15) / 16 - 1);
                var tag = quadWords | (UInt64)DMATag.IdType.RET << 28;
                BitConverter.TryWriteBytes(result.AsSpan(0, 8), tag);
            }

            return result;
        }
    }

    /// <summary>
    /// Packs 16 bit components, two per word
    /// </summary>
    public static IEnumerable<UInt32> Pack16(IEnumerable<Int32[]> vectors)
    {
        foreach (var vector in vectors)
        {
            yield return (UInt32)(vector[0] & 0xFFFF) | (UInt32)(vector[1] & 0xFFFF) << 16;
            yield return (UInt32)(vector[2] & 0xFFFF) | (UInt32)(vector[3] & 0xFFFF) << 16;
        }
    }

    /// <summary>
    /// Packs 8 bit components, four per word
    /// </summary>
    public static IEnumerable<UInt32> Pack8(IEnumerable<Int32[]> vectors)
    {
        foreach (var vector in vectors)
        {
            yield return (UInt32)(vector[0] & 0xFF) | (UInt32)(vector[1] & 0xFF) << 8 | (UInt32)(vector[2] & 0xFF) << 16 | (UInt32)(vector[3] & 0xFF) << 24;
        }
    }
}
