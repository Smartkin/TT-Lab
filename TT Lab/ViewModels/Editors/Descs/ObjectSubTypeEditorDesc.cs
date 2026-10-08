using System.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors.Descs;

/// <summary>
/// An object's sub type picked from what the game makes of it for the object's type, listed again when the type changes
/// </summary>
public record ObjectSubTypeEditorDesc : EditorDesc
{
    protected override DocumentNodeViewModel ConstructInternal()
    {
        var typeNode = Node.Parent?.Children.FirstOrDefault(child => child.Name == nameof(GameObjectData.Type));
        ITwinObject.ObjectType TypeOf() => typeNode?.GetValue<ITwinObject.ObjectType>() ?? ITwinObject.ObjectType.GenericObject;
        return new ChoiceFieldViewModel(Document, Node, () => ObjectTypes.SubTypesOf(TypeOf()), value => ObjectTypes.FindSubType(TypeOf(), value), typeNode);
    }
}
