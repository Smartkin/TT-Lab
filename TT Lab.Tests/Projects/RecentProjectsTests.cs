using Newtonsoft.Json.Linq;
using TT_Lab.Project;

namespace TT_Lab.Tests.Projects;

// The File menu's Open Recent lists the projects opened last, newest first
public sealed class RecentProjectsTests
{
    private static readonly string Projects = Path.Combine(Path.GetTempPath(), "TT Lab Projects");

    private static string Project(int number) => Path.Combine(Projects, $"Project {number}");

    [Fact]
    public void ProjectsOpenedAgainGoBackToTheTop()
    {
        var recents = ProjectManager.WithRecentlyOpened([Project(1), Project(2), Project(3)], Project(2) + Path.DirectorySeparatorChar);

        Assert.Equal([Project(2), Project(1), Project(3)], recents);
    }

    [Fact]
    public void OnlyTheLastTenAreKept()
    {
        var recents = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            recents = ProjectManager.WithRecentlyOpened(recents, Project(i));
        }

        Assert.Equal(Enumerable.Range(2, 10).Reverse().Select(Project), recents);
    }

    // Settings are read into a dictionary of objects, the list stays a JSON array until it's asked for
    [Fact]
    public void TheListIsReadBackFromTheSettingsFile()
    {
        var previous = Preferences.GetPreference<List<string>>(Preferences.RecentProjects);
        try
        {
            Preferences.SetPreference(Preferences.RecentProjects, new JArray(Project(1), Project(2)));

            Assert.Equal([Project(1), Project(2)], Preferences.GetPreference<List<string>>(Preferences.RecentProjects));
        }
        finally
        {
            Preferences.SetPreference(Preferences.RecentProjects, previous);
        }
    }
}
