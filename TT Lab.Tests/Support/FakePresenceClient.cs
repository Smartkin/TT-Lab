using TT_Lab.Tools.Discord;

namespace TT_Lab.Tests.Support;

// A Discord connection that connects, fails and gets shown what the test says
internal sealed class FakePresenceClient : IPresenceClient
{
    public event Action? Ready;
    public event Action<string>? Failed;

    public bool Connected { get; private set; }
    public List<PresenceActivity> Shown { get; } = [];
    public bool Disposed { get; private set; }

    public void Connect() => Connected = true;
    public void Show(PresenceActivity activity) => Shown.Add(activity);
    public void Dispose() => Disposed = true;

    public void RaiseReady() => Ready?.Invoke();
    public void RaiseFailed() => Failed?.Invoke("no Discord");
}
