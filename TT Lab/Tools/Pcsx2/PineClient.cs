using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace TT_Lab.Tools.Pcsx2;

public enum PineStatus : UInt32
{
    Running = 0,
    Paused = 1,
    Shutdown = 2
}

/// <summary>
/// PCSX2 answers requests for memory with a failure until its virtual machine runs
/// </summary>
public sealed class PineRefusedException() : IOException("PCSX2 turned the request down");

/// <summary>
/// PCSX2's remote control (PINE): a request is its whole size, an opcode and its arguments, an answer its size, 0 for success and
/// the result. It reads and writes the game's memory while it runs. A Unix socket on Linux, a TCP port on Windows
/// </summary>
public sealed class PineClient(Stream stream) : IDisposable
{
    private const byte Read8 = 0;
    private const byte Read32 = 2;
    private const byte Read64 = 3;
    private const byte Write8 = 4;
    private const byte Write32 = 6;
    private const byte Status = 15;

    private readonly object _lock = new();

    public static PineClient ConnectUnix(string socketPath)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new PineClient(new NetworkStream(socket, true) { ReadTimeout = 5000, WriteTimeout = 5000 });
    }

    public static PineClient ConnectTcp(int port)
    {
        var client = new TcpClient();
        try
        {
            client.Connect("127.0.0.1", port);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        var stream = client.GetStream();
        stream.ReadTimeout = 5000;
        stream.WriteTimeout = 5000;
        return new PineClient(stream);
    }

    public byte ReadByte(UInt32 address) => Call(Read8, address)[0];

    public UInt32 ReadUInt32(UInt32 address) => BinaryPrimitives.ReadUInt32LittleEndian(Call(Read32, address));

    public UInt64 ReadUInt64(UInt32 address) => BinaryPrimitives.ReadUInt64LittleEndian(Call(Read64, address));

    public void WriteByte(UInt32 address, byte value) => Call(Write8, address, [value]);

    public void WriteUInt32(UInt32 address, UInt32 value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Call(Write32, address, bytes);
    }

    public PineStatus GetStatus()
    {
        return (PineStatus)BinaryPrimitives.ReadUInt32LittleEndian(Send([Status]));
    }

    /// <summary>
    /// A NUL ended string of the game's memory, read a word at a time
    /// </summary>
    public string ReadString(UInt32 address, int maxLength = 256)
    {
        var text = new StringBuilder();
        Span<byte> word = stackalloc byte[4];
        for (var offset = 0u; text.Length < maxLength; offset += 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(word, ReadUInt32(address + offset));
            foreach (var character in word)
            {
                if (character == 0 || text.Length == maxLength)
                {
                    return text.ToString();
                }

                text.Append((char)character);
            }
        }

        return text.ToString();
    }

    private byte[] Call(byte opcode, UInt32 address, ReadOnlySpan<byte> arguments = default)
    {
        var request = new byte[5 + arguments.Length];
        request[0] = opcode;
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(1), address);
        arguments.CopyTo(request.AsSpan(5));
        return Send(request);
    }

    private byte[] Send(ReadOnlySpan<byte> request)
    {
        lock (_lock)
        {
            var message = new byte[4 + request.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(message, (UInt32)message.Length);
            request.CopyTo(message.AsSpan(4));
            stream.Write(message);
            stream.Flush();

            var size = new byte[4];
            stream.ReadExactly(size);
            var answer = new byte[BinaryPrimitives.ReadUInt32LittleEndian(size) - 4];
            stream.ReadExactly(answer);
            if (answer.Length == 0 || answer[0] != 0)
            {
                throw new PineRefusedException();
            }

            return answer[1..];
        }
    }

    public void Dispose()
    {
        stream.Dispose();
    }
}
