using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Tools;

// TT Lab started with a project's folder, the way Blender's Open in TT Lab starts it when none runs, and the model file to open in it
public sealed class CommandLineTests
{
    [Fact]
    public void TheProjectsFolderAndTheFileToOpen()
    {
        Assert.Equal(("/mods/Mod", "/mods/Mod/assets/Mod/OGI/Crash.tlm"), ShellViewModel.ParseCommandLine(["/mods/Mod", "--open-file", "/mods/Mod/assets/Mod/OGI/Crash.tlm"]));
        Assert.Equal(("/mods/Mod", "/mods/Mod/x.tlm"), ShellViewModel.ParseCommandLine(["--open-file", "/mods/Mod/x.tlm", "/mods/Mod"]));
        Assert.Equal(("/mods/Mod", null), ShellViewModel.ParseCommandLine(["/mods/Mod"]));
        Assert.Equal((null, null), ShellViewModel.ParseCommandLine([]));
    }
}
