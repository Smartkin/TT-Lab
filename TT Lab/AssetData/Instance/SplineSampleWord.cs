using System;

namespace TT_Lab.AssetData.Instance;

/// <summary>
/// A camera spline sample's W, a word of the game's (CameraSplineCamera::SampleWord, SampleShortValue, SampleByteValue): bit 24 passes
/// over the sample, else it's a key whose bits 8-23 are an offset along the curve in 50/32768ths of a unit from -50 and bits 0-7 a
/// share of the way toward the target in 5/128ths from -5. Nothing reads bits 25-31, they're kept
/// </summary>
public static class SplineSampleWord
{
    public const UInt32 Passes = 1U << 24;
    // A key of neither offset nor share, like the ends of the game's splines
    public const UInt32 NeutralKey = 0x7FFF80;
    public const Single OffsetUnit = 50.0f / 32768;
    public const Single ShareUnit = 5.0f / 128;
    private const UInt32 Values = 0xFFFFFF;
    private const UInt32 Unread = 1U << 25;

    public static bool IsKey(UInt32 word) => (word & Passes) == 0;

    public static Single Offset(UInt32 word) => (word >> 8 & 0xFFFF) * OffsetUnit - 50.0f;

    public static Single Share(UInt32 word) => (word & 0xFF) * ShareUnit - 5.0f;

    // A sample passing over without values becomes a key that changes nothing
    public static UInt32 WithKey(UInt32 word, bool isKey)
    {
        if (!isKey)
        {
            return word | Passes;
        }

        word &= ~Passes;
        return (word & Values) == 0 ? word | NeutralKey : word;
    }

    /// <summary>
    /// The word as it's stored: a W of 0 is a new sample's (CameraGeometry.KeepSampleWords), a key of the lowest offset and share gets
    /// bit 25, which nothing reads
    /// </summary>
    public static UInt32 Stored(UInt32 word) => word == 0 ? Unread : word;

    public static UInt32 WithOffset(UInt32 word, Single offset)
    {
        if (Single.IsNaN(offset))
        {
            return word;
        }

        var steps = (UInt32)Math.Clamp(MathF.Round((offset + 50.0f) / OffsetUnit), 0, 0xFFFF);
        return word & ~0xFFFF00U | steps << 8;
    }

    public static UInt32 WithShare(UInt32 word, Single share)
    {
        if (Single.IsNaN(share))
        {
            return word;
        }

        var steps = (UInt32)Math.Clamp(MathF.Round((share + 5.0f) / ShareUnit), 0, 0xFF);
        return word & ~0xFFU | steps;
    }

    public static UInt32 Of(Single w) => BitConverter.SingleToUInt32Bits(w);

    public static Single ToW(UInt32 word) => BitConverter.UInt32BitsToSingle(word);
}
