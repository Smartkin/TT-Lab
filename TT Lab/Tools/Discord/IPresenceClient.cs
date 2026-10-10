using System;

namespace TT_Lab.Tools.Discord;

// A connection to the Discord app on this computer, made for one attempt: its events come from any thread, and once it failed or
// got closed it's disposed and another one is made for the next attempt
internal interface IPresenceClient : IDisposable
{
    event Action? Ready;
    event Action<string>? Failed;

    void Connect();
    void Show(PresenceActivity activity);
}
