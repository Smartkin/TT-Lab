using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace Twinsanity.Libraries
{
    public static class Hasher
    {
        private const Int32 BufferSize = 0x10000;

        public static String ComputeHash(Stream stream)
        {
            return ComputeHash(stream, stream.Length - stream.Position);
        }

        public static String ComputeHash(Stream stream, UInt32 length)
        {
            return ComputeHash(stream, (Int64)length);
        }

        public static String ComputeHash(Byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static String ComputeHash(Stream stream, Int64 length)
        {
            var streamPos = stream.Position;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<Byte>.Shared.Rent(BufferSize);
            try
            {
                var remaining = length;
                while (remaining > 0)
                {
                    var read = stream.Read(buffer, 0, (Int32)Math.Min(buffer.Length, remaining));
                    if (read == 0)
                    {
                        break;
                    }

                    sha.AppendData(buffer, 0, read);
                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<Byte>.Shared.Return(buffer);
            }

            stream.Position = streamPos;
            return Convert.ToHexString(sha.GetHashAndReset());
        }
    }
}
