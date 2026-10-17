using TT_Lab.Assets;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Blender;

namespace TT_Lab.Tests.Tools;

// Every TT Lab of the computer keeps its blend files in one file of the state folder: written one after the other
[Collection(ProjectCollection.Name)]
public sealed class BlendFilesTests : IDisposable
{
    private readonly string _state = Path.Combine(Path.GetTempPath(), "TT Lab Tests", "Blend files " + Guid.NewGuid().ToString("N"));
    private readonly string _folder = BlendFiles.Folder;

    public BlendFilesTests()
    {
        Directory.CreateDirectory(_state);
        BlendFiles.Folder = _state;
    }

    public void Dispose()
    {
        BlendFiles.Folder = _folder;
        Directory.Delete(_state, true);
    }

    [Fact]
    public async Task AnotherTTLabWritingIsWaitedFor()
    {
        var project = Guid.NewGuid();
        var model = new LabURI("res://Test/OGI/Crash");
        var blend = Path.Combine(_state, "Crash.blend");
        File.WriteAllText(blend, string.Empty);
        var path = Path.Combine(_state, BlendFiles.FileName);
        File.WriteAllText(path, "{}");
        var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var release = Task.Run(async () =>
        {
            await Task.Delay(150);
            await held.DisposeAsync();
        });

        BlendFiles.Remember(project, model, blend);
        await release;

        Assert.Equal(blend, BlendFiles.Of(project, model));
    }

    // The file Blender has open reached through a link to its folder is the file picked
    [Fact]
    public void AFileReachedThroughALinkedFolderIsTheSameFile()
    {
        var real = Path.Combine(_state, "real");
        var linked = Path.Combine(_state, "linked");
        Directory.CreateDirectory(real);
        File.WriteAllText(Path.Combine(real, "x.blend"), string.Empty);
        try
        {
            Directory.CreateSymbolicLink(linked, real);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Windows makes links only for administrators and developer mode
            return;
        }

        Assert.True(BlendFiles.IsSameFile(Path.Combine(linked, "x.blend"), Path.Combine(real, "x.blend")));
        Assert.False(BlendFiles.IsSameFile(Path.Combine(linked, "y.blend"), Path.Combine(real, "x.blend")));
    }

    // The blend file has no model of a project any more: none of its models opens it, the other projects' still do
    [Fact]
    public void AProjectsModelsForgetABlendFile()
    {
        var project = Guid.NewGuid();
        var other = Guid.NewGuid();
        var blend = Path.Combine(_state, "level.blend");
        var own = Path.Combine(_state, "own.blend");
        File.WriteAllText(blend, string.Empty);
        File.WriteAllText(own, string.Empty);
        BlendFiles.Remember(project, new LabURI("res://A/OGI/Crash"), blend);
        BlendFiles.Remember(project, new LabURI("res://A/OGI/Cortex"), own);
        BlendFiles.Remember(other, new LabURI("res://B/OGI/Nina"), blend);

        BlendFiles.ForgetProject(project, blend);

        Assert.Null(BlendFiles.Of(project, new LabURI("res://A/OGI/Crash")));
        Assert.Equal(own, BlendFiles.Of(project, new LabURI("res://A/OGI/Cortex")));
        Assert.Equal(blend, BlendFiles.Of(other, new LabURI("res://B/OGI/Nina")));
    }
}
