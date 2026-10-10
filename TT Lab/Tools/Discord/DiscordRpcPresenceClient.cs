using System;
using DiscordRPC;

namespace TT_Lab.Tools.Discord;

// Lachee's DiscordRichPresence, through the Discord app's local socket (a named pipe on Windows; on Linux in the runtime folder,
// the Flatpak's and the Snap's included). It retries on its own every half a second up to a minute and says nothing when the socket
// just goes away, its next failed attempt does: the first failure or close ends this client. Its own log isn't kept, it's 60 lines an
// attempt without Discord (every place it looks for the socket)
internal sealed class DiscordRpcPresenceClient : IPresenceClient
{
    private readonly DiscordRpcClient _client;

    public event Action? Ready;
    public event Action<string>? Failed;

    public DiscordRpcPresenceClient(string applicationId)
    {
        _client = new DiscordRpcClient(applicationId) { SkipIdenticalPresence = true };
        _client.OnReady += (_, _) => Ready?.Invoke();
        _client.OnConnectionFailed += (_, _) => Failed?.Invoke("Discord isn't running or can't be reached");
        _client.OnClose += (_, close) => Failed?.Invoke($"Discord closed the connection: {close.Reason} ({close.Code})");
        _client.OnError += (_, error) => Log.WriteLine($"Discord Rich Presence: {error.Message} ({error.Code})", Log.LogType.Trace);
    }

    public void Connect() => _client.Initialize();

    public void Show(PresenceActivity activity)
    {
        _client.SetPresence(new RichPresence
        {
            Details = activity.Details,
            State = activity.State,
            Timestamps = activity.Start is { } start ? new Timestamps(start) : null,
            Assets = new DiscordRPC.Assets { LargeImageKey = DiscordPresence.LogoKey }
        });
    }

    public void Dispose() => _client.Dispose();
}
