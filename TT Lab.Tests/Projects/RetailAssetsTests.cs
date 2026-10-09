using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.Controls;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// A project shared without the game's assets (a repository can't have them) has its own packages but not Global PS2, PS2, Global XBOX and
// XBOX: the versions its packages link to are needed, unpacked from the game's files when it opens, from the preferences' folders without
// asking when they have the game's files. A project opened from another folder than the one it was made in is where it's opened
[Collection(ProjectCollection.Name)]
public sealed class RetailAssetsTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly List<string> _temporary = [];

    public void Dispose()
    {
        foreach (var directory in _temporary.Where(Directory.Exists))
        {
            Directory.Delete(directory, true);
        }

        _project.Dispose();
    }

    private string ProjectFile => Path.Combine(_project.Project.ProjectPath, "Test.tson");

    private string Assets => Path.Combine(_project.Project.ProjectPath, "assets");

    // Shared without the game's packages
    private void TakeOutTheGamesPackages(params string[] packages)
    {
        _project.Project.Serialize();
        foreach (var package in packages.Length > 0 ? packages : ["Global PS2_Test", "PS2_Test", "Global XBOX_Test", "XBOX_Test"])
        {
            Directory.Delete(Path.Combine(Assets, package), true);
        }
    }

    private void WriteOwnFile(string package, string name, string text)
    {
        var directory = Path.Combine(Assets, package, "GameObject");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), text);
    }

    private string FakeDisc(GamePlatform platform)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, platform == GamePlatform.Xbox ? "default.xbe" : "SYSTEM.CNF"), "");
        _temporary.Add(directory);
        return directory;
    }

    private static RetailAssets.Version VersionOf(RetailAssets.Check check, GamePlatform platform) => check.Versions.Single(version => version.Platform == platform);

    [Fact]
    public void TheVersionsTheProjectsPackagesLinkToAreNeeded()
    {
        TakeOutTheGamesPackages();
        // The project's own package depends on every version's packages whatever it has
        var check = RetailAssets.Of(ProjectFile, "Test");
        Assert.Null(check.Problem);
        Assert.True(check.IsMissingAny);
        Assert.All(check.Versions, version => Assert.True(version.Missing));
        Assert.All(check.Versions, version => Assert.False(version.Required));

        WriteOwnFile("Test", "Crate.data", "{\"ObjectId\":{\"_uri\":\"res://Global PS2_Test/GameObject/CRATE\"}}");
        check = RetailAssets.Of(ProjectFile, "Test");
        Assert.True(VersionOf(check, GamePlatform.PS2).Required);
        Assert.False(VersionOf(check, GamePlatform.Xbox).Required);

        // A package of the project depending on a version's package needs it, a package whose name only starts like one doesn't count
        Directory.CreateDirectory(Path.Combine(Assets, "Mod"));
        File.WriteAllText(Path.Combine(Assets, "Mod", "Mod.json"), "{\"Dependencies\":[{\"_uri\":\"res://XBOX_Test\"}],\"Other\":\"res://XBOX_Testing/x\"}");
        Assert.All(RetailAssets.Of(ProjectFile, "Test").Versions, version => Assert.True(version.Required));
    }

    [Fact]
    public void AProjectWithTheGamesPackagesNeedsNothingAndOneWithHalfOfAVersionsCantGetThemBack()
    {
        _project.Project.Serialize();
        Assert.False(RetailAssets.Of(ProjectFile, "Test").IsMissingAny);

        TakeOutTheGamesPackages("PS2_Test");
        Assert.Contains("Global PS2_Test", RetailAssets.Of(ProjectFile, "Test").Problem);
    }

    [Fact]
    public void ThePreferencesFoldersDoWhenTheyHaveWhatsNeeded()
    {
        TakeOutTheGamesPackages();
        WriteOwnFile("Test", "Crate.data", "\"res://PS2_Test/GameObject/CRATE\"");
        var check = RetailAssets.Of(ProjectFile, "Test");
        var ps2 = FakeDisc(GamePlatform.PS2);
        var xbox = FakeDisc(GamePlatform.Xbox);

        Assert.True(RetailAssets.IsDisc(GamePlatform.PS2, ps2));
        Assert.False(RetailAssets.IsDisc(GamePlatform.PS2, xbox));
        Assert.False(RetailAssets.IsDisc(GamePlatform.Xbox, null));
        // The PS2 version is needed, the Xbox one isn't
        var onlyXbox = RetailAssets.FromPreferences(check, platform => platform == GamePlatform.Xbox ? xbox : AppContext.BaseDirectory);
        Assert.Equal([GamePlatform.Xbox], onlyXbox.Keys);
        Assert.False(RetailAssets.Suffice(check, onlyXbox));
        var both = RetailAssets.FromPreferences(check, platform => platform == GamePlatform.Xbox ? xbox : ps2);
        Assert.True(RetailAssets.Suffice(check, both));
        Assert.True(RetailAssets.Suffice(check, new Dictionary<GamePlatform, string> { [GamePlatform.PS2] = ps2 }));

        // Packages that need neither take either
        File.Delete(Path.Combine(Assets, "Test", "GameObject", "Crate.data"));
        check = RetailAssets.Of(ProjectFile, "Test");
        Assert.True(RetailAssets.Suffice(check, onlyXbox));
        Assert.False(RetailAssets.Suffice(check, new Dictionary<GamePlatform, string>()));
    }

    [AvaloniaFact]
    public async Task OpeningAProjectWithoutTheGamesPackagesAsksForTheGamesFiles()
    {
        TakeOutTheGamesPackages();
        WriteOwnFile("Test", "Crate.data", "\"res://Global PS2_Test/GameObject/CRATE\"");
        var before = File.ReadAllBytes(ProjectFile);
        var manager = Locator.Current.GetService<ProjectManager>()!;
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), manager));
        var asked = new List<RetailDiscsDialogue.Request>();
        manager.PreferredDiscFolder = _ => AppContext.BaseDirectory;
        manager.AskForRetailDiscs = request =>
        {
            asked.Add(request);
            return Task.FromResult<IReadOnlyDictionary<GamePlatform, string>?>(null);
        };
        manager.OpenedProject = null;

        manager.OpenProject(_project.Project.ProjectPath);
        for (var waited = 0; waited < 20000 && asked.Count == 0; waited += 20)
        {
            await Task.Delay(20);
        }

        await Task.Delay(200);
        var request = Assert.Single(asked);
        Assert.True(VersionOf(request.Check, GamePlatform.PS2) is { Missing: true, Required: true });
        Assert.True(VersionOf(request.Check, GamePlatform.Xbox) is { Missing: true, Required: false });
        Assert.Empty(request.Found);
        // Cancelled, it isn't opened and nothing is written
        Assert.Null(manager.OpenedProject);
        Assert.Equal(before, File.ReadAllBytes(ProjectFile));
        Assert.False(Directory.Exists(Path.Combine(Assets, "Global PS2_Test")));
        Log.SetViewModel(null);
    }

    // The game's files go into the project's disc folder by their place on the disc, the disc's folder given with a separator at its end
    // or without: without one the PS2 disc's folders went to the root of the file system
    [Fact]
    public void TheGamesFilesAreCopiedIntoTheProjectsDiscFolder()
    {
        var disc = FakeDisc(GamePlatform.PS2);
        Directory.CreateDirectory(Path.Combine(disc, "Crash6"));
        File.WriteAllText(Path.Combine(disc, "Crash6", "Crash.BH"), "header");
        var xbox = FakeDisc(GamePlatform.Xbox);
        Directory.CreateDirectory(Path.Combine(xbox, "Startup"));
        File.WriteAllText(Path.Combine(xbox, "Startup", "Default.rmx"), "chunk");
        var project = _project.Project;
        project.DiscContentPathPS2 = disc;
        project.DiscContentPathXbox = xbox + Path.DirectorySeparatorChar;

        project.CopyDiscContents();

        Assert.Equal("header", File.ReadAllText(Path.Combine(project.ProjectPath, "disc", "ps2", "Crash6", "Crash.BH")));
        Assert.True(File.Exists(Path.Combine(project.ProjectPath, "disc", "ps2", "SYSTEM.CNF")));
        Assert.Equal("chunk", File.ReadAllText(Path.Combine(project.ProjectPath, "disc", "xbox", "Startup", "Default.rmx")));
        Assert.Equal($"{project.ProjectPath}/disc/ps2", project.DiscContentPathPS2);
        Assert.Equal($"{project.ProjectPath}/disc/xbox", project.DiscContentPathXbox);
    }

    [Fact]
    public void AProjectOpenedFromAnotherFolderIsWhereItsOpened()
    {
        var project = _project.Project;
        project.DiscContentPathPS2 = Path.Combine(project.ProjectPath, "disc", "ps2");
        project.DiscContentPathXbox = "/somewhere/else/xbox";
        project.Serialize();
        var elsewhere = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"), "cloned repository");
        _temporary.Add(Path.GetDirectoryName(elsewhere)!);
        Directory.CreateDirectory(elsewhere);
        File.Copy(ProjectFile, Path.Combine(elsewhere, "Test.tson"));

        var opened = TT_Lab.Project.Project.ReadProjectFile(Path.Combine(elsewhere, "Test.tson"));

        Assert.Equal(elsewhere, opened.ProjectPath);
        Assert.Equal(Path.GetDirectoryName(elsewhere), opened.Path);
        Assert.Equal("Test", opened.Name);
        // The game's files copied into it are in its disc folder, what's elsewhere stays there
        Assert.Equal(Path.Combine(elsewhere, "disc", "ps2"), opened.DiscContentPathPS2);
        Assert.Equal("/somewhere/else/xbox", opened.DiscContentPathXbox);
    }

    // The project's file has the paths of the system it was made on: Linux took a Windows path for the name of one file, and the disc
    // folders of a project made there were lost. Copying the disc wrote its folder after the project's with a slash, and Windows doesn't
    // tell names apart by case
    [Theory]
    [InlineData(@"C:\Users\Someone\TT Projects", @"c:\users\someone\TT Projects\Test/disc/ps2", @"D:\Twinsanity\Xbox")]
    [InlineData("/home/someone/TT Projects", "/home/someone/TT Projects/Test/disc/ps2", "/mnt/discs/Twinsanity Xbox")]
    public void DiscFoldersOfAProjectMadeOnEitherSystemAreWhereItsOpened(string madeIn, string ps2, string xbox)
    {
        _project.Project.Serialize();
        var file = JsonNode.Parse(File.ReadAllText(ProjectFile))!;
        file["Path"] = madeIn;
        file["DiscContentPathPS2"] = ps2;
        file["DiscContentPathXbox"] = xbox;
        var elsewhere = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"), "Test");
        _temporary.Add(Path.GetDirectoryName(elsewhere)!);
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(Path.Combine(elsewhere, "Test.tson"), file.ToJsonString());

        var opened = TT_Lab.Project.Project.ReadProjectFile(Path.Combine(elsewhere, "Test.tson"));

        Assert.Equal(elsewhere, opened.ProjectPath);
        Assert.Equal(Path.Combine(elsewhere, "disc", "ps2"), opened.DiscContentPathPS2);
        Assert.Equal(xbox, opened.DiscContentPathXbox);
    }
}
