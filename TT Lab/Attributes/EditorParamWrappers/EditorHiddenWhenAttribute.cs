using System.Reflection;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes.EditorParamWrappers;

public class EditorHiddenWhenAttribute(string conditionMember) : EditorParamWrapperBaseAttribute
{
    private const BindingFlags ConditionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public override void ApplyTo(DocumentNodeViewModel viewModel)
    {
        var target = viewModel.Property.Target;
        var type = target.GetType();
        var condition = type.GetProperty(conditionMember, ConditionFlags)?.GetValue(target)
                        ?? type.GetField(conditionMember, ConditionFlags)?.GetValue(target);
        if (condition is true)
        {
            viewModel.IsVisible = false;
        }
    }
}
