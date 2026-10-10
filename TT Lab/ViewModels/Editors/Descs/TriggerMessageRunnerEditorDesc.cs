using TT_Lab.AssetData.Code.Object;

namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// The behaviour runner a trigger message's behaviour starts on, picked by what it does
/// </summary>
public record TriggerMessageRunnerEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ChoiceFieldViewModel(Document, Node, ObjectTriggerBehaviourData.Runners, ObjectTriggerBehaviourData.FindRunner);
}
