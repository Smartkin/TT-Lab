using TT_Lab.AssetData.Instance.Scenery;

namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// A scenery's fog picked from the game's tables
/// </summary>
public record FogColorEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal() => new ChoiceFieldViewModel(Document, Node, SceneryFog.Tables, SceneryFog.Find);
}
