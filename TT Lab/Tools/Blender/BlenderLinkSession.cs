using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// One Blender linked to TT Lab (LINK.md): a hello with the server's token first, then requests handled on the UI thread (which asks
/// the editors) and pushes of what TT Lab changes of the assets the Blender's models use. What it made or changed that it didn't save
/// goes when it drops or the socket closes
/// </summary>
public sealed class BlenderLinkSession
{
    private readonly BlenderLinkServer _server;
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _writing = new(1, 1);
    private long _nextId = 1;
    private bool _closed;

    public BlenderLinkSession(BlenderLinkServer server, TcpClient client)
    {
        _server = server;
        _client = client;
        _stream = client.GetStream();
        Assets = new BlenderLinkAssets(this);
    }

    /// <summary>
    /// The blend file's UUID, what its hello said
    /// </summary>
    public string Blend { get; private set; } = string.Empty;

    /// <summary>
    /// The blend file it has open, what its hello and its saves said (none for a new one): Use an Existing Blend File of it asks this
    /// Blender instead of starting another on it
    /// </summary>
    public string BlendPath { get; internal set; } = string.Empty;

    public bool IsWelcomed { get; private set; }

    /// <summary>
    /// What this Blender made and changed in the project, and the models it linked
    /// </summary>
    public BlenderLinkAssets Assets { get; }

    internal BlenderLinkServer Server => _server;

    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = IsWelcomed ? await LinkProtocol.ReadAsync(_stream, token) : await LinkProtocol.ReadAsync(_stream, token, LinkProtocol.MostHelloHeader, 0);
                if (frame == null)
                {
                    break;
                }

                if (!IsWelcomed)
                {
                    if (!await Welcome(frame, token))
                    {
                        break;
                    }

                    continue;
                }

                await Answer(frame, token);
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or ObjectDisposedException or SocketException)
        {
            if (exception is InvalidDataException)
            {
                Log.WriteLine($"The Blender link of {Blend} sent what TT Lab can't read: {exception.Message}", Log.LogType.Warning);
            }
        }
        finally
        {
            try
            {
                await OnUiThread(() =>
                {
                    Assets.Drop();
                    return true;
                });
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Taking back what it didn't save failed: the session goes all the same, Edit in Blender of its models would ask a closed link
                Log.WriteLine($"Taking back what the Blender of {Blend} didn't save failed: {exception.Message}", Log.LogType.Warning);
                Log.WriteLine(exception.ToString(), Log.LogType.Debug);
            }
            finally
            {
                Close();
                _server.Forget(this);
            }
        }
    }

    private async Task<bool> Welcome(LinkProtocol.Frame frame, CancellationToken token)
    {
        if (frame.Type != "hello" || !_server.Accepts(frame.Header["token"]?.GetValue<string>()))
        {
            await Send(new JsonObject { ["type"] = "error", ["re"] = frame.Id, ["text"] = "Not the token of this TT Lab" }, null, token);
            return false;
        }

        if (frame.Header["protocol"] is not JsonValue protocol || !protocol.TryGetValue<int>(out var version) || version != LinkProtocol.Version)
        {
            await Send(new JsonObject { ["type"] = "error", ["re"] = frame.Id, ["text"] = $"This TT Lab links with protocol {LinkProtocol.Version}: install its add-on" }, null, token);
            return false;
        }

        Blend = frame.Header["blend"]?.GetValue<string>() ?? string.Empty;
        BlendPath = frame.Header["path"] is JsonValue path && path.TryGetValue<string>(out var opened) ? opened : string.Empty;
        IsWelcomed = true;
        await Send(new JsonObject
        {
            ["type"] = "welcome",
            ["re"] = frame.Id,
            ["project"] = _server.ProjectJson(),
            ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty
        }, null, token);
        var addon = frame.Header["addon"]?.GetValue<string>();
        Log.WriteLine($"Blender linked ({frame.Header["blender"]?.GetValue<string>() ?? "a Blender"}, add-on {addon ?? "of no version"})");
        // An add-on of another version may write what this TT Lab reads otherwise
        if (BlenderAddonInstaller.BundledZip() is { } zip && BlenderAddonInstaller.VersionOf(zip) is { } bundled && (!Version.TryParse(addon, out var linked) || linked != bundled))
        {
            Log.WriteLine($"Blender has the add-on {addon}, TT Lab comes with {bundled}: install it with Tools > Install the Blender add-on", Log.LogType.Warning);
        }

        return true;
    }

    private async Task Answer(LinkProtocol.Frame frame, CancellationToken token)
    {
        (JsonObject Header, byte[]? Payload) answer;
        try
        {
            answer = await OnUiThreadAsync(() => Assets.HandleAsync(frame));
        }
        // Whatever fails is the answer, the link stays
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.WriteLine($"Blender's {frame.Type} failed: {exception.Message}", Log.LogType.Warning);
            Log.WriteLine(exception.ToString(), Log.LogType.Debug);
            answer = (new JsonObject { ["type"] = "error", ["text"] = exception.Message }, null);
        }

        answer.Header["re"] = frame.Id;
        await Send(answer.Header, answer.Payload, token);
    }

    /// <summary>
    /// Tells this Blender what TT Lab changed
    /// </summary>
    public void Push(JsonObject header, byte[]? payload = null)
    {
        if (_closed)
        {
            return;
        }

        _ = Send(header, payload, CancellationToken.None);
    }

    private async Task Send(JsonObject header, byte[]? payload, CancellationToken token)
    {
        await _writing.WaitAsync(token);
        try
        {
            header["id"] = Interlocked.Increment(ref _nextId);
            await LinkProtocol.WriteAsync(_stream, header, payload, token);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or SocketException or OperationCanceledException)
        {
            _closed = true;
        }
        finally
        {
            _writing.Release();
        }
    }

    public void Close()
    {
        if (_closed)
        {
            return;
        }

        try
        {
            LinkProtocol.WriteAsync(_stream, new JsonObject { ["type"] = "closing", ["id"] = Interlocked.Increment(ref _nextId) }, null, CancellationToken.None)
                .Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception exception) when (exception is IOException or AggregateException or ObjectDisposedException)
        {
        }

        _closed = true;
        _client.Close();
    }

    private static Task<T> OnUiThread<T>(Func<T> action)
    {
        return Application.Current != null && !Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread.InvokeAsync(action).GetTask() : Task.FromResult(action());
    }

    private static Task<T> OnUiThreadAsync<T>(Func<Task<T>> action)
    {
        return Application.Current != null && !Dispatcher.UIThread.CheckAccess() ? Dispatcher.UIThread.InvokeAsync(action) : action();
    }
}
