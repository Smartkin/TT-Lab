using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Tests.Editor;

// A trigger message's behaviour starts on one of the instance's two behaviour runners (the game's bit 24, BehaviourCallerIndex), picked
// by what it does: the main runner, where the instance's spawn script runs, or the second one, alongside it
[Collection(ProjectCollection.Name)]
public sealed class TriggerMessageRunnerTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [AvaloniaFact]
    public void TheRunnerIsPickedByWhatItDoes()
    {
        var drone = _project.Add(new GameObject(), "DRONE", 0x10, _project.Project.Ps2Package);
        var data = new GameObjectData(drone) { Name = "DRONE" };
        data.TriggerBehaviours.Add(new ObjectTriggerBehaviourData { MessageID = 286 });
        drone.SetData(data);
        var document = new DocumentViewModel(drone);
        document.Initialize();
        var node = document.PropertyGraph.Find("Root.AssetData.TriggerBehaviours[0].BehaviourCallerIndex")!;
        var field = Assert.IsType<ChoiceFieldViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        field.Activator.Activate();

        Assert.Equal("Behaviour Runner", field.Caption);
        Assert.Equal(["Replace Current Behaviour", "Run in parallel"], field.Choices.Select(choice => choice.Name));
        Assert.Equal("Replace Current Behaviour", field.SelectedChoice!.Name);

        field.SelectedChoice = ObjectTriggerBehaviourData.FindRunner(1);
        Assert.Equal(1, data.TriggerBehaviours[0].BehaviourCallerIndex);
        Assert.Equal("Run in parallel", field.SelectedChoice!.Name);

        document.Undo();
        Assert.Equal(0, data.TriggerBehaviours[0].BehaviourCallerIndex);
        Assert.Equal("Replace Current Behaviour", field.SelectedChoice!.Name);
    }
}
