using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The link to the Blender add-on (LINK.md): frames over a socket on the loopback, each the header's length and the payload's length
/// (unsigned 32 bit little endian), the header (JSON in UTF-8: its type, its id and, for an answer, the re of the message it answers)
/// and the payload (a model file, a picture). A running TT Lab tells the add-on how to reach it in a discovery file of its state folder
/// </summary>
public static class LinkProtocol
{
    public const int Version = 1;
    public const int MostHeader = 16 * 1024 * 1024;
    public const int MostPayload = 1024 * 1024 * 1024;
    // Before the token is checked a frame is a hello: anyone can connect to the port, and a frame's buffers are made before it's read
    public const int MostHelloHeader = 64 * 1024;

    public sealed record Frame(JsonObject Header, byte[] Payload)
    {
        public string Type => Header["type"]?.GetValue<string>() ?? string.Empty;

        public long Id => Header["id"] is JsonValue id && id.TryGetValue<long>(out var value) ? value : 0;

        public long? Re => Header["re"] is JsonValue re && re.TryGetValue<long>(out var value) ? value : null;
    }

    public static async Task WriteAsync(Stream stream, JsonObject header, byte[]? payload, CancellationToken token)
    {
        var text = Encoding.UTF8.GetBytes(header.ToJsonString());
        var lengths = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(lengths.AsSpan(0, 4), (UInt32)text.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(lengths.AsSpan(4, 4), (UInt32)(payload?.Length ?? 0));
        await stream.WriteAsync(lengths, token);
        await stream.WriteAsync(text, token);
        if (payload is { Length: > 0 })
        {
            await stream.WriteAsync(payload, token);
        }

        await stream.FlushAsync(token);
    }

    /// <summary>
    /// The next frame, none once the other side closed the socket
    /// </summary>
    public static async Task<Frame?> ReadAsync(Stream stream, CancellationToken token, int mostHeader = MostHeader, int mostPayload = MostPayload)
    {
        var lengths = new byte[8];
        if (!await ReadExactlyAsync(stream, lengths, token))
        {
            return null;
        }

        var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(lengths.AsSpan(0, 4));
        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(lengths.AsSpan(4, 4));
        if (headerLength > mostHeader || payloadLength > mostPayload)
        {
            throw new InvalidDataException($"A frame of a {headerLength} byte header and a {payloadLength} byte payload isn't the add-on's");
        }

        var text = new byte[headerLength];
        var payload = new byte[payloadLength];
        if (!await ReadExactlyAsync(stream, text, token) || !await ReadExactlyAsync(stream, payload, token))
        {
            return null;
        }

        JsonNode? header;
        try
        {
            header = JsonNode.Parse(text);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"A header that isn't JSON: {exception.Message}");
        }

        if (header is not JsonObject json || json["type"] is not JsonValue type || !type.TryGetValue<string>(out _))
        {
            throw new InvalidDataException("A header without a type");
        }

        return new Frame(json, payload);
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), token);
            if (count == 0)
            {
                return false;
            }

            read += count;
        }

        return true;
    }

    /// <summary>
    /// Where TT Lab keeps what the add-on reads of it: the blender folder of its state folder, like its logs'
    /// </summary>
    public static string StateFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TT Lab", "Blender");
        }

        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
        if (string.IsNullOrEmpty(stateHome) || !Path.IsPathRooted(stateHome))
        {
            stateHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state");
        }

        return Path.Combine(stateHome, "tt-lab", "blender");
    }
}
