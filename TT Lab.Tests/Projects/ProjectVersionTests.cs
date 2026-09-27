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

    [Fact]
    public void ProjectsWithoutAVersionDoNotOpen()
    {
        EditProjectFile(json => json.Remove("Version"));

        Assert.Throws<ProjectException>(() => TT_Lab.Project.Project.Deserialize(_projectFile));
    }
}
