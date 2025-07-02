using SharpHash.Base;
using System;
using System.IO;

namespace Twinsanity.Libraries
{
    public static class Hasher
    {
        public static String ComputeHash(Stream stream)
        {
            var streamPos = stream.Position;
            var result = HashFactory.Crypto.CreateSHA2_256().ComputeStream(stream).ToString();
            stream.Position = streamPos;
            return result;
        }

        public static String ComputeHash(Stream stream, UInt32 length)
        {
            var streamPos = stream.Position;
            var result = HashFactory.Crypto.CreateSHA2_256().ComputeStream(stream, length).ToString();
            stream.Position = streamPos;
            return result;
        }

        public static String ComputeHash(Byte[] bytes)
        {
            return HashFactory.Crypto.CreateSHA2_256().ComputeBytes(bytes).ToString();
        }
    }
}
