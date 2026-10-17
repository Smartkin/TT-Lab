using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using TT_Lab.Tools.Blender;

namespace TT_Lab.Tests.Support;

/// <summary>
/// The add-on's end of the Blender link (LINK.md): asks and reads the answer, keeping what TT Lab tells meanwhile
/// </summary>
internal sealed class TestBlender : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly Channel<LinkProtocol.Frame?> _frames = Channel.CreateUnbounded<LinkProtocol.Frame?>();
    private readonly List<LinkProtocol.Frame> _pushed = [];
    private long _nextId;

    private TestBlender(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _ = Task.Run(Read);
    }

    public static async Task<TestBlender> Connect(int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);
        return new TestBlender(client);
    }

    /// <summary>
    /// The add-on's hello, with the blend file it has open when given
    /// </summary>
    public static JsonObject Hello(string token, int protocol = LinkProtocol.Version, string? path = null, string blend = "blend")
    {
        var hello = new JsonObject { ["type"] = "hello", ["token"] = token, ["protocol"] = protocol, ["blend"] = blend, ["blender"] = "5.2.2", ["addon"] = "1.0.0" };
        if (path != null)
        {
            hello["path"] = path;
        }

        return hello;
    }

    private async Task Read()
    {
        try
        {
            while (await LinkProtocol.ReadAsync(_stream, CancellationToken.None) is { } frame)
            {
                await _frames.Writer.WriteAsync(frame);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }

        await _frames.Writer.WriteAsync(null);
    }

    public async Task<LinkProtocol.Frame> Ask(JsonObject header, byte[]? payload = null)
    {
        var id = ++_nextId;
        header["id"] = id;
        await LinkProtocol.WriteAsync(_stream, header, payload, CancellationToken.None);
        while (true)
        {
            var frame = await Next() ?? throw new IOException("TT Lab closed the link");
            if (frame.Re == id)
            {
                return frame;
            }

            _pushed.Add(frame);
        }
    }

    public IEnumerable<LinkProtocol.Frame> Pushed(string type)
    {
        while (_frames.Reader.TryRead(out var frame) && frame != null)
        {
            _pushed.Add(frame);
        }

        return _pushed.Where(frame => frame.Type == type).ToList();
    }

    public async Task<LinkProtocol.Frame> WaitFor(string type)
    {
        if (Pushed(type).FirstOrDefault() is { } pushed)
        {
            _pushed.Remove(pushed);
            return pushed;
        }

        while (await Next() is { } frame)
        {
            if (frame.Type == type)
            {
                return frame;
            }

            _pushed.Add(frame);
        }

        throw new IOException($"TT Lab closed the link before telling {type}");
    }

    public async Task<bool> IsClosed()
    {
        while (await Next() is not null)
        {
        }

        return true;
    }

    private async Task<LinkProtocol.Frame?> Next()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _frames.Reader.ReadAsync(timeout.Token);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
