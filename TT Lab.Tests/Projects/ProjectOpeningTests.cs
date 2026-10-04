using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// A project opens with what other programs left among its files, or says which file it can't read
[Collection(ProjectCollection.Name)]
public sealed class ProjectOpeningTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private string ProjectFile => Path.Combine(_project.Project.ProjectPath, "Test.tson");

    private void Open()
    {
        // The assets are read in parallel, a copy of one used to keep every other file waiting for good
        var opening = Task.Run(() => TT_Lab.Project.Project.Deserialize(ProjectFile));
        Assert.True(opening.Wait(TimeSpan.FromSeconds(60)), "Opening the project never ended");
    }

    [Fact]
    public void ACopiedAssetFileIsLeftOut()
    {
        var crab = _project.Add(new GameObject(), "Crab", 0x10);
        crab.SetData(new GameObjectData(crab) { Name = "CRAB" });
        _project.Project.Serialize();
        var file = Directory.GetFiles(_project.AssetsPath, "Crab.json", SearchOption.AllDirectories).Single();
        File.Copy(file, Path.Combine(Path.GetDirectoryName(file)!, "Copy of Crab.json"));

        Open();

        var opened = Locator.Current.GetService<ProjectManager>()!.OpenedProject!;
        Assert.NotSame(_project.Project, opened);
        Assert.Equal("CRAB", ((IAsset)opened.AssetManager.GetAsset<GameObject>(crab.URI)).GetData<GameObjectData>().Name);
    }

    [Fact]
    public void AnUnreadableAssetFileIsNamed()
    {
        _project.Project.Serialize();
        var broken = Path.Combine(Directory.GetDirectories(_project.AssetsPath)[0], "Broken.json");
        File.WriteAllText(broken, "{ not json");

        var exception = Assert.ThrowsAny<AggregateException>(() => TT_Lab.Project.Project.Deserialize(ProjectFile));

        var reason = Assert.IsType<ProjectException>(exception.Flatten().InnerExceptions[0]);
        // Opening reads the assets relative to the project
        Assert.Contains(Path.GetRelativePath(_project.Project.ProjectPath, broken), reason.Message);
    }

    // Duplicating an instance in the project tree asked for the ID generators of a chunk only an open tab registered
    [Fact]
    public void InstancesOfAChunkWithoutATabGetIds()
    {
        var generator = TwinIdGeneratorServiceProvider.GetGeneratorForChunk<ObjectInstance>("levels/earth/hub/closed", _project.Project.BasePackage.URI, Enums.Layouts.LAYER_2);

        Assert.Equal(0U, generator.GenerateTwinId());
    }
}
