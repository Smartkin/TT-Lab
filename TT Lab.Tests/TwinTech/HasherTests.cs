using System.Security.Cryptography;
using Twinsanity.Libraries;

namespace TT_Lab.Tests.TwinTech;

public class HasherTests
{
    private static readonly byte[] Data = Enumerable.Range(0, 0x30000).Select(i => (byte)(i * 7 + (i >> 8))).ToArray();

    [Fact]
    public void BytesAreHashedAsUppercaseSha256()
    {
        var hash = Hasher.ComputeHash(Data);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Data)), hash);
        Assert.Equal(hash.ToUpperInvariant(), hash);
    }

    [Fact]
    public void StreamIsHashedFromItsPositionToTheEnd()
    {
        using var stream = new MemoryStream(Data);
        stream.Position = 0x100;

        var hash = Hasher.ComputeHash(stream);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Data.AsSpan(0x100))), hash);
        Assert.Equal(0x100, stream.Position);
    }

    // Sections hash only their own bytes even though the rest of the archive follows them in the stream
    [Fact]
    public void StreamWithLengthOnlyHashesThatManyBytes()
    {
        using var stream = new MemoryStream(Data);
        stream.Position = 0x10;

        var hash = Hasher.ComputeHash(stream, 0x12345u);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Data.AsSpan(0x10, 0x12345))), hash);
        Assert.Equal(0x10, stream.Position);
    }

    [Fact]
    public void LengthPastTheEndHashesTheRestOfTheStream()
    {
        using var stream = new MemoryStream(Data);
        stream.Position = Data.Length - 5;

        var hash = Hasher.ComputeHash(stream, 100u);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(Data.AsSpan(Data.Length - 5))), hash);
    }
}
