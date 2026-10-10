using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tests.Editor;

// Playing a chunk in PCSX2 from its toolbar: only the PS2 version's level chunks play, Reload and Stop only do something while the game
// runs, and the settings are kept in the preferences
[Collection(ProjectCollection.Name)]
public sealed class GameLaunchTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Pcsx2Service _service;

    public GameLaunchTests()
    {
        _service = new Pcsx2Service(Locator.Current.GetService<ProjectManager>()!);
        Locator.CurrentMutable.RegisterConstant(_service);
    }

    public void Dispose()
    {
        Locator.CurrentMutable.UnregisterAll<Pcsx2Service>();
        _service.Dispose();
        _project.Dispose();
    }

    private LevelChunk AddChunk(string path, Package package) => _project.Add(new LevelChunk { AdditionalPath = path }, path.Split('/')[^1], package: package);

    private static ViewportViewModel Open(LevelChunk chunk)
    {
        var document = new DocumentViewModel(chunk);
        document.Initialize();
        var viewport = new ViewportViewModel();
        viewport.Init(document);
        Dispatcher.UIThread.RunJobs();
        return viewport;
    }

    [Fact]
    public void OnlyThePs2VersionsLevelChunksPlay()
    {
        Assert.Null(Pcsx2Service.WhyNotPlayable(AddChunk("levels/earth/totem/l03beach", _project.Project.Ps2Package)));
        Assert.Equal("Only the PS2 version's chunks play in PCSX2", Pcsx2Service.WhyNotPlayable(AddChunk("levels/earth/hub/beach", _project.Project.XboxPackage)));
        var startup = _project.Add(new LevelChunk { AdditionalPath = "startup" }, "default", package: _project.Project.GlobalPackagePS2);
        Assert.Equal("The startup chunk can't be played, open a level's chunk", Pcsx2Service.WhyNotPlayable(startup));
        Assert.StartsWith("The game has room for chunk paths of up to 47 characters", Pcsx2Service.WhyNotPlayable(AddChunk($"levels/earth/{new string('a', 40)}", _project.Project.Ps2Package)));
    }

    [AvaloniaFact]
    public void TheToolbarPlaysAPs2ChunkAndOnlyReloadsOrStopsARunningGame()
    {
        var viewport = Open(AddChunk("levels/earth/totem/l03beach", _project.Project.Ps2Package));
        try
        {
            Assert.True(viewport.CanPlayInGame);
            Assert.True(((ICommand)viewport.PlayInGameCommand).CanExecute(null));
            Assert.False(((ICommand)viewport.ReloadInGameCommand).CanExecute(null));
            Assert.False(((ICommand)viewport.StopGameCommand).CanExecute(null));
            Assert.False(viewport.IsGameRunning);
            Assert.False(viewport.IsPlayingThisChunk);
        }
        finally
        {
            viewport.Close();
        }
    }

    [AvaloniaFact]
    public void AChunkThatCantBePlayedSaysWhyOnItsPlayButton()
    {
        var viewport = Open(AddChunk("levels/earth/hub/beach", _project.Project.XboxPackage));
        try
        {
            Assert.False(viewport.CanPlayInGame);
            Assert.False(((ICommand)viewport.PlayInGameCommand).CanExecute(null));
            Assert.Equal("Only the PS2 version's chunks play in PCSX2", viewport.PlayInGameHint);
        }
        finally
        {
            viewport.Close();
        }
    }

    [AvaloniaFact]
    public void ThePreferencesKeepWhatIsPickedAndForgetIt()
    {
        var preferences = new PreferencesViewModel(new TT_Lab.Tools.Discord.DiscordPresence(() => default, () => new FakePresenceClient(), action => action(), () => DateTime.UtcNow));
        var (path, image, reload) = (Preferences.GetPreference<string>(Preferences.Pcsx2Path), Preferences.GetPreference<string>(Preferences.Pcsx2DiscImage),
            Preferences.GetPreference<bool>(Preferences.Pcsx2ReloadOnSave));
        try
        {
            preferences.Pcsx2Path = "/opt/pcsx2/pcsx2-qt";
            preferences.Pcsx2DiscImage = "/games/Crash Twinsanity (Europe).iso";
            preferences.ReloadGameAfterSaving = false;
            Assert.Equal("/opt/pcsx2/pcsx2-qt", Preferences.GetPreference<string>(Preferences.Pcsx2Path));
            Assert.Equal("/games/Crash Twinsanity (Europe).iso", Preferences.GetPreference<string>(Preferences.Pcsx2DiscImage));
            Assert.False(Preferences.GetPreference<bool>(Preferences.Pcsx2ReloadOnSave));

            preferences.ForgetPcsx2();
            preferences.ForgetDiscImage();
            Assert.Equal("", Preferences.GetPreference<string>(Preferences.Pcsx2Path));
            Assert.Equal("", Preferences.GetPreference<string>(Preferences.Pcsx2DiscImage));
            Assert.False(string.IsNullOrEmpty(preferences.FoundPcsx2));
        }
        finally
        {
            Preferences.SetPreference(Preferences.Pcsx2Path, path);
            Preferences.SetPreference(Preferences.Pcsx2DiscImage, image);
            Preferences.SetPreference(Preferences.Pcsx2ReloadOnSave, reload);
        }
    }
}
