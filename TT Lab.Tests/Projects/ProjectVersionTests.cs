using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.Project;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Projects;

[Collection(ProjectCollection.Name)]
public sealed class ProjectVersionTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly string _projectFile;

    public ProjectVersionTests()
    {
        _project.Project.Serialize();
        _projectFile = Path.Combine(_project.Project.ProjectPath, "Test.tson");
    }

    public void Dispose() => _project.Dispose();

    private void EditProjectFile(Action<JObject> edit)
    {
        var json = JObject.Parse(File.ReadAllText(_projectFile));
        edit(json);
        File.WriteAllText(_projectFile, json.ToString());
    }

    [Fact]
    public void ProjectsOfTheCurrentVersionOpen()
    {
        TT_Lab.Project.Project.Deserialize(_projectFile);

        var opened = Locator.Current.GetService<ProjectManager>()!.OpenedProject!;
        Assert.NotSame(_project.Project, opened);
        Assert.Equal(_project.Project.Version, opened.Version);
    }

    [Fact]
    public void ProjectsOfAnotherVersionDoNotOpen()
    {
        EditProjectFile(json => json["Version"] = "0.6.0");

        var exception = Assert.Throws<ProjectException>(() => TT_Lab.Project.Project.Deserialize(_projectFile));
        Assert.Contains("0.6.0", exception.Message);
    }

    // The project's ID is made once: it had no setter, so it was never read back and every write of the project file had another
    [Fact]
    public void TheProjectsIdStaysThroughOpeningAndSaving()
    {
        var id = _project.Project.UUID;
        Assert.Equal(id, Guid.Parse((string)JObject.Parse(File.ReadAllText(_projectFile))["UUID"]!));

        var read = TT_Lab.Project.Project.ReadProjectFile(_projectFile);
        Assert.Equal(id, read.UUID);

        _project.Project.Serialize();
        Assert.Equal(id, Guid.Parse((string)JObject.Parse(File.ReadAllText(_projectFile))["UUID"]!));
    }

    // A project made without a version's disc has that version's packages empty and off, the Xbox version's always were but the PS2's
    // stayed on in a project of the Xbox disc alone
    [Fact]
    public void AVersionMadeWithoutItsDiscIsOff()
    {
        var project = _project.Project;
        project.DiscContentPathPS2 = null;
        using var gate = new MemoryGate(1L << 30);

        project.UnpackAssetsPS2(gate);

        Assert.False(project.GlobalPackagePS2.Enabled);
        Assert.False(project.Ps2Package.Enabled);
    }

    [Fact]
    public void ProjectsWithoutAVersionDoNotOpen()
    {
        EditProjectFile(json => json.Remove("Version"));

        Assert.Throws<ProjectException>(() => TT_Lab.Project.Project.Deserialize(_projectFile));
    }
}
