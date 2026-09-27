using TT_Lab.Project;
using TT_Lab.Services.Implementations;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Editor;

public sealed class ProjectCreationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"TTLabCreation_{Guid.NewGuid():N}");
    private readonly string _previousPs2Path = Preferences.GetPreference<string>(Preferences.Ps2DiscContentPath);

    public void Dispose()
    {
        Preferences.SetPreference(Preferences.Ps2DiscContentPath, _previousPs2Path);
        Preferences.SetPreference(Preferences.XboxDiscContentPath, "");
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Disc(string name, string? file)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        if (file != null)
        {
            File.WriteAllText(Path.Combine(path, file), string.Empty);
        }

        return path;
    }

    private static ProjectCreationViewModel Create() => new(null!, new ProjectManager(new TestProject.NullEventAggregator()), new DataValidatorService());

    private static bool HasErrors(ProjectCreationViewModel viewModel, string property) => viewModel.GetErrors(property).Cast<object>().Any();

    // Either version's disc is enough, a path that's given has to hold that version's files
    [Fact]
    public void EitherDiscIsEnough()
    {
        var viewModel = Create();

        viewModel.PS2DiscContentPath = "";
        viewModel.XboxDiscContentPath = "";
        Assert.True(HasErrors(viewModel, nameof(viewModel.PS2DiscContentPath)));
        Assert.True(HasErrors(viewModel, nameof(viewModel.XboxDiscContentPath)));

        viewModel.XboxDiscContentPath = Disc("xbox", "default.xbe");
        Assert.False(HasErrors(viewModel, nameof(viewModel.PS2DiscContentPath)));
        Assert.False(HasErrors(viewModel, nameof(viewModel.XboxDiscContentPath)));

        viewModel.PS2DiscContentPath = Disc("ps2", "readme.txt");
        Assert.True(HasErrors(viewModel, nameof(viewModel.PS2DiscContentPath)));
        Assert.False(HasErrors(viewModel, nameof(viewModel.XboxDiscContentPath)));

        viewModel.PS2DiscContentPath = Disc("ps2 disc", "SYSTEM.CNF");
        Assert.False(HasErrors(viewModel, nameof(viewModel.PS2DiscContentPath)));
    }
}
