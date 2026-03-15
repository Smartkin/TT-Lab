using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Attributes;

public interface IFieldChange
{
    void DataChanged(PropertyNode listeningNode, PropertyNode changedNode);
}