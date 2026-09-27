using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.AgentLab;

namespace TT_Lab.Tests.Editor;

// An object's AgentLab commands can only be a list of commands, most objects have none
[Collection(ProjectCollection.Name)]
public sealed class ObjectCommandsEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private DocumentViewModel OpenObject(string commands)
    {
        var gameObject = _project.Add(new GameObject(), "Crate", 0x3);
        gameObject.SetData(new GameObjectData(gameObject) { BehaviourPack = commands });
        var document = new DocumentViewModel(gameObject);
        document.Initialize();
        return document;
    }

    // Scripts get checked on the task pool once the typing stops
    private static AgentLabCompiler.CompilerStatus Check(DocumentViewModel document)
    {
        var editor = Assert.IsType<CodeEditorViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.BehaviourPack")!).Construct());
        var initial = editor.CodeStatus;
        new Window { Content = new ContentControl { Content = editor }, Width = 800, Height = 600 }.Show();
        var watch = Stopwatch.StartNew();
        while (ReferenceEquals(initial, editor.CodeStatus) && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Assert.NotSame(initial, editor.CodeStatus);
        return editor.CodeStatus;
    }

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("SetSurface(0x01FF0008);\n")]
    public void CommandsOrNoneAreValid(string commands)
    {
        var status = Check(OpenObject(commands));

        Assert.False(status.IsError, status.Message);
    }

    [AvaloniaFact]
    public void AnythingElseIsInvalid()
    {
        var status = Check(OpenObject("behaviour COM_CRATE {\n}\n"));

        Assert.True(status.IsError);
        Assert.Contains("Only commands can be written here", status.Message);
    }

    // Worked out when building from whether the object is a startup object in a level (BehaviourStarterTests)
    [AvaloniaFact]
    public void WhetherObjectsListTheirResourcesIsNotEdited()
    {
        var document = OpenObject(string.Empty);

        Assert.NotNull(document.PropertyGraph.Find("Root.AssetData.BehaviourPack"));
        Assert.Null(document.PropertyGraph.Find("Root.AssetData.ReferencesResources"));
    }
}
