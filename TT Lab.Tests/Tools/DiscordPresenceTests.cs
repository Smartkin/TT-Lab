using System.Runtime.CompilerServices;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Discord;

namespace TT_Lab.Tests.Tools;

// What Discord shows while TT Lab is used: a line for what's done and one for the editor in front, a timer from when the project's tree
// showed up, a break after 10 minutes away from TT Lab, and a connection made again 5 minutes after it failed
public sealed class DiscordPresenceTests : IDisposable
{
    private static readonly DateTime Opened = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private readonly bool _wasEnabled = Preferences.GetPreference<bool>(Preferences.DiscordRichPresence);

    public void Dispose() => Preferences.SetPreference(Preferences.DiscordRichPresence, _wasEnabled);

    // Only the type of the asset matters
    private static IAsset Of<T>() where T : IAsset => (IAsset)RuntimeHelpers.GetUninitializedObject(typeof(T));

    [Fact]
    public void EveryEditorSaysWhatItsFor()
    {
        Assert.Equal("Editing chunk...", PresenceActivity.LineFor(Of<LevelChunk>()));
        Assert.Equal("Looking at a model...", PresenceActivity.LineFor(Of<OGI>()));
        Assert.Equal("Looking at a model...", PresenceActivity.LineFor(Of<SaveIcon>()));
        Assert.Equal("Labbing new behavior...", PresenceActivity.LineFor(Of<BehaviourGraph>()));
        Assert.Equal("Labbing new behavior...", PresenceActivity.LineFor(Of<BehaviourCommandsSequence>()));
        Assert.Equal("Swapping textures...", PresenceActivity.LineFor(Of<Texture>()));
        Assert.Equal("Admiring the Skydome...", PresenceActivity.LineFor(Of<Skydome>()));
        Assert.Equal("Listening to nice sounds...", PresenceActivity.LineFor(Of<SoundEffect>()));
        Assert.Equal("Listening to nice sounds...", PresenceActivity.LineFor(Of<SoundEffectFR>()));
        Assert.Equal("Making good fonts...", PresenceActivity.LineFor(Of<Font>()));
        Assert.Equal("Changing texts...", PresenceActivity.LineFor(Of<TextFile>()));
        Assert.Equal("Changing materials...", PresenceActivity.LineFor(Of<Material>()));
        Assert.Equal("Editing game objects...", PresenceActivity.LineFor(Of<GameObject>()));
        Assert.Equal("Tinkering with pictures...", PresenceActivity.LineFor(Of<PSM>()));
        Assert.Equal("Tinkering with pictures...", PresenceActivity.LineFor(Of<PTC>()));
        Assert.Equal("Changing the game globally...", PresenceActivity.LineFor(Of<Mesh>()));
        Assert.Equal("Changing the game globally...", PresenceActivity.LineFor(Of<InstanceTemplate>()));
        Assert.Equal("Changing the game globally...", PresenceActivity.LineFor(Of<CollisionSurface>()));
        Assert.Equal("Changing the game globally...", PresenceActivity.LineFor(Of<DefaultParticles>()));
        Assert.Equal("Messing with UI sounds...", PresenceActivity.LineFor(Of<UiSoundLibrary>()));
        Assert.Null(PresenceActivity.LineFor(Of<Package>()));
        Assert.Null(PresenceActivity.LineFor(null));
    }

    private static PresenceInputs Ready(IAsset? editor = null, bool focus = true, bool game = false) => new(ProjectPhase.Ready, Opened, editor, game, focus);

    [Fact]
    public void TheLinesFollowTheProjectAndTheEditorInFront()
    {
        var tracker = new PresenceTracker();

        Assert.Equal(new PresenceActivity(PresenceActivity.Deciding, null, null), tracker.Follow(new PresenceInputs(ProjectPhase.None, null, null, false, true), Opened));
        // Creating or opening one, until its tree shows up, without a timer
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, null, null), tracker.Follow(new PresenceInputs(ProjectPhase.Loading, null, null, false, true), Opened));
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, null, Opened), tracker.Follow(Ready(), Opened.AddMinutes(1)));
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, "Swapping textures...", Opened), tracker.Follow(Ready(Of<Texture>()), Opened.AddMinutes(2)));
        // The game Play started (its build included) goes over the editor, the timer goes on
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, PresenceActivity.Testing, Opened), tracker.Follow(Ready(Of<LevelChunk>(), game: true), Opened.AddMinutes(3)));
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, null, Opened), tracker.Follow(Ready(Of<Package>()), Opened.AddMinutes(4)));
    }

    [Fact]
    public void TenMinutesAwayFromTtLabIsABreakThatStartsTheTimerOver()
    {
        var tracker = new PresenceTracker();
        tracker.Follow(Ready(Of<OGI>()), Opened);
        Assert.Equal("Looking at a model...", tracker.Follow(Ready(Of<OGI>(), focus: false), Opened.AddMinutes(1)).State);
        Assert.Equal("Looking at a model...", tracker.Follow(Ready(Of<OGI>(), focus: false), Opened.AddMinutes(10.9)).State);

        Assert.Equal(new PresenceActivity(PresenceActivity.OnBreak, null, null), tracker.Follow(Ready(Of<OGI>(), focus: false), Opened.AddMinutes(11)));
        Assert.Equal(new PresenceActivity(PresenceActivity.OnBreak, null, null), tracker.Follow(Ready(Of<OGI>(), focus: false), Opened.AddMinutes(40)));
        var back = Opened.AddMinutes(41);
        Assert.Equal(new PresenceActivity(PresenceActivity.Making, "Looking at a model...", back), tracker.Follow(Ready(Of<OGI>()), back));
        Assert.Equal(back, tracker.Follow(Ready(Of<OGI>()), back.AddMinutes(5)).Start);
    }

    [Fact]
    public void ThePlayedGameIsNoBreakAndNeitherIsNoProject()
    {
        var tracker = new PresenceTracker();
        tracker.Follow(Ready(), Opened);
        Assert.Equal(PresenceActivity.Testing, tracker.Follow(Ready(focus: false, game: true), Opened.AddMinutes(1)).State);
        Assert.Equal(PresenceActivity.Testing, tracker.Follow(Ready(focus: false, game: true), Opened.AddMinutes(30)).State);
        // The 10 minutes count from when the game stopped
        Assert.Equal(PresenceActivity.Making, tracker.Follow(Ready(focus: false), Opened.AddMinutes(31)).Details);
        Assert.Equal(PresenceActivity.Making, tracker.Follow(Ready(focus: false), Opened.AddMinutes(40)).Details);
        Assert.Equal(PresenceActivity.OnBreak, tracker.Follow(Ready(focus: false), Opened.AddMinutes(41)).Details);

        var closed = new PresenceTracker();
        Assert.Equal(PresenceActivity.Deciding, closed.Follow(new PresenceInputs(ProjectPhase.None, null, null, false, false), Opened).Details);
        Assert.Equal(PresenceActivity.Deciding, closed.Follow(new PresenceInputs(ProjectPhase.None, null, null, false, false), Opened.AddHours(1)).Details);
        // A project opened again counts from its own tree
        var reopened = Opened.AddHours(2);
        Assert.Equal(reopened, closed.Follow(new PresenceInputs(ProjectPhase.Ready, reopened, null, false, false), reopened).Start);
    }

    [Fact]
    public void ItConnectsWhileOnAndTriesAgainFiveMinutesAfterAFailure()
    {
        var now = Opened;
        var inputs = Ready(Of<Texture>());
        var clients = new List<FakePresenceClient>();
        var connections = new List<PresenceConnection>();
        var presence = new DiscordPresence(() => inputs, () =>
        {
            var client = new FakePresenceClient();
            clients.Add(client);
            return client;
        }, action => action(), () => now);
        presence.ConnectionChanged += () => connections.Add(presence.Connection);

        Preferences.SetPreference(Preferences.DiscordRichPresence, false);
        presence.Tick();
        Assert.Empty(clients);
        Assert.Equal(PresenceConnection.Off, presence.Connection);

        Preferences.SetPreference(Preferences.DiscordRichPresence, true);
        presence.Tick();
        var first = Assert.Single(clients);
        Assert.True(first.Connected);
        Assert.Equal(PresenceConnection.Connecting, presence.Connection);

        first.RaiseReady();
        Assert.Equal(PresenceConnection.Connected, presence.Connection);
        Assert.Equal([new PresenceActivity(PresenceActivity.Making, "Swapping textures...", Opened)], first.Shown);

        // Discord takes 5 updates in 20 seconds, changes in between wait
        inputs = Ready(Of<Material>());
        now = now.AddSeconds(2);
        presence.Tick();
        Assert.Single(first.Shown);
        now = now.AddSeconds(2);
        presence.Tick();
        Assert.Equal("Changing materials...", first.Shown[^1].State);
        presence.Tick();
        Assert.Equal(2, first.Shown.Count);

        first.RaiseFailed();
        Assert.Equal(PresenceConnection.NotConnected, presence.Connection);
        Assert.True(SpinWait.SpinUntil(() => first.Disposed, TimeSpan.FromSeconds(5)));
        // An event of a client let go of changes nothing
        first.RaiseReady();
        Assert.Equal(PresenceConnection.NotConnected, presence.Connection);
        now = now.AddMinutes(4.9);
        presence.Tick();
        Assert.Single(clients);
        now = now.AddMinutes(0.1);
        presence.Tick();
        Assert.Equal(2, clients.Count);
        Assert.Equal(PresenceConnection.Connecting, presence.Connection);
        clients[1].RaiseReady();
        Assert.Equal("Changing materials...", Assert.Single(clients[1].Shown).State);

        Preferences.SetPreference(Preferences.DiscordRichPresence, false);
        presence.Tick();
        Assert.Equal(PresenceConnection.Off, presence.Connection);
        Assert.True(SpinWait.SpinUntil(() => clients[1].Disposed, TimeSpan.FromSeconds(5)));
        Assert.Equal([PresenceConnection.Connecting, PresenceConnection.Connected, PresenceConnection.NotConnected, PresenceConnection.Connecting,
            PresenceConnection.Connected, PresenceConnection.Off], connections);
    }
}
