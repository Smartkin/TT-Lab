using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using Path = System.IO.Path;
using TT_Lab.Project;
using TT_Lab.Project.Build;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Build;

// A build profile picks the version of the game and the chunks left out, links to them are dropped from the built chunks' files
[Collection(ProjectCollection.Name)]
public sealed class BuildProfileTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private LevelChunk AddChunk(string path, Package? package = null)
    {
        return _project.Add(new LevelChunk { AdditionalPath = path }, path.Split('/')[^1], package: package);
    }

    [Fact]
    public void ProfilesAreSavedLoadedAndDeleted()
    {
        var library = new BuildProfileLibrary(_project.Project);
        var profile = new BuildProfile { Name = "Beach only", Platform = TT_Lab.Project.Project.GamePlatform.Xbox, ExcludedChunks = ["res://a", "res://b"] };

        library.Save(profile);
        library.Save(new BuildProfile { Name = "Default" });

        var loaded = library.Load();
        Assert.Equal(["Beach only", "Default"], loaded.Select(item => item.Name));
        Assert.Equal(TT_Lab.Project.Project.GamePlatform.Xbox, loaded[0].Platform);
        Assert.Equal(["res://a", "res://b"], loaded[0].ExcludedChunks);
        Assert.Equal([new LabURI("res://a"), new LabURI("res://b")], loaded[0].ExcludedChunkUris());

        // Renaming moves the file, deleting removes it
        loaded[0].Name = "Beach";
        library.Save(loaded[0]);
        Assert.Equal(["Beach", "Default"], library.Load().Select(item => item.Name));
        library.Delete(loaded[0]);
        Assert.Equal(["Default"], library.Load().Select(item => item.Name));
    }

    [Fact]
    public void LinksToChunksLeftOutAreDroppedFromTheFileAndKeptInTheAsset()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var cave = AddChunk("levels/earth/hub/cave");
        var links = _project.Add(new ChunkLinks { Chunk = "default" }, "Links");
        links.SetData(new ChunkLinksData(links) { Links = [new ChunkLink { Path = beach.URI }, new ChunkLink { Path = cave.URI }] });
        var factory = new PS2ItemFactory { ExcludedChunks = new HashSet<LabURI> { cave.URI } }.ForChunk();

        var item = (Twinsanity.TwinsanityInterchange.Interfaces.Items.SM.ITwinLink)((IAsset)links).GetData<ChunkLinksData>().Export(factory);

        Assert.Single(item.LinksList);
        Assert.Equal("levels/earth/hub/beach", item.LinksList[0].Path);
        Assert.Equal([beach.URI, cave.URI], factory.LinkedChunks);
        Assert.Equal(2, ((IAsset)links).GetData<ChunkLinksData>().Links.Count);

        // Without a profile every link goes out
        var plain = new PS2ItemFactory().ForChunk();
        Assert.Equal(2, ((Twinsanity.TwinsanityInterchange.Interfaces.Items.SM.ITwinLink)((IAsset)links).GetData<ChunkLinksData>().Export(plain)).LinksList.Count);
    }

    [Fact]
    public void AChunksFileIsOnlyUpToDateWhileTheSameLinksAreDropped()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var cave = AddChunk("levels/earth/hub/cave");
        var output = Path.Combine(_project.Project.ProjectPath, "build", "archives", "Levels", "hub.rm2");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllBytes(output, [1, 2, 3]);
        var cache = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);
        cache.ExcludedChunks = new HashSet<LabURI> { cave.URI };

        cache.Record("chunk:hub", [], [output], [beach.URI, cave.URI]);

        Assert.True(cache.IsUpToDate("chunk:hub", [output]));
        cache.ExcludedChunks = new HashSet<LabURI>();
        Assert.False(cache.IsUpToDate("chunk:hub", [output]));
        cache.ExcludedChunks = new HashSet<LabURI> { beach.URI, cave.URI };
        Assert.False(cache.IsUpToDate("chunk:hub", [output]));
        // Leaving out a chunk it doesn't link to changes nothing
        cache.ExcludedChunks = new HashSet<LabURI> { cave.URI, new LabURI("res://elsewhere") };
        Assert.True(cache.IsUpToDate("chunk:hub", [output]));

        // The choice survives saving the manifest
        cache.Save();
        var reloaded = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);
        reloaded.ExcludedChunks = new HashSet<LabURI> { cave.URI };
        Assert.True(reloaded.IsUpToDate("chunk:hub", [output]));
        reloaded.ExcludedChunks = new HashSet<LabURI>();
        Assert.False(reloaded.IsUpToDate("chunk:hub", [output]));
    }

    [AvaloniaFact]
    public void TheDialogKeepsAProfilesChunksAndVersion()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var cave = AddChunk("levels/earth/hub/cave");
        var xboxHub = AddChunk("levels/earth/hub/hub", _project.Project.GlobalPackageXbox);
        _project.Add(new LevelChunk(), "default");
        var library = new BuildProfileLibrary(_project.Project);

        var dialog = new BuildDialogViewModel(_project.Project, library);

        // Without profiles there's a default one with every chunk of the PS2 version in it
        Assert.Equal(["Default"], dialog.Profiles.Select(profile => profile.Name));
        Assert.True(dialog.IsPs2);
        Assert.Equal([beach.URI, cave.URI], dialog.ShownChunks.Select(row => row.Uri));
        Assert.All(dialog.ShownChunks, row => Assert.True(row.IsIncluded));

        dialog.NewProfileCommand.Execute().Subscribe();
        Assert.Equal("Profile 2", dialog.ProfileName);
        dialog.ProfileName = "No cave";
        dialog.ShownChunks.Single(row => row.Uri == cave.URI).IsIncluded = false;
        dialog.BuildCommand.Execute().Subscribe();

        Assert.NotNull(dialog.Result);
        Assert.Equal("No cave", dialog.Result!.Name);
        Assert.Equal(TT_Lab.Project.Project.GamePlatform.PS2, dialog.Result.Platform);
        Assert.Equal([cave.URI.ToString()], dialog.Result.ExcludedChunks);
        // The default profile isn't written until it gets built with or saved
        Assert.Equal(["No cave"], library.Load().Select(profile => profile.Name));

        // Opening the dialog again finds the profile last built with, its chunks as they were, and the Xbox version has its own chunks
        var reopened = new BuildDialogViewModel(_project.Project, library);
        Assert.Equal("No cave", reopened.SelectedProfile!.Name);
        Assert.False(reopened.ShownChunks.Single(row => row.Uri == cave.URI).IsIncluded);
        Assert.True(reopened.ShownChunks.Single(row => row.Uri == beach.URI).IsIncluded);
        reopened.IsXbox = true;
        Assert.Equal([xboxHub.URI], reopened.ShownChunks.Select(row => row.Uri));
        reopened.Filter = "nothing";
        Assert.Empty(reopened.ShownChunks);
    }
}
