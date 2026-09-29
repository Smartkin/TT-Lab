using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Attributes;

public interface IFieldChange
{
    void DataChanged(PropertyNode listeningNode, PropertyNode changedNode);

    // Runs when a document links the fields. The values are the asset's own then, so only what editors show about them (a field made
    // read-only) gets set up: reactors writing values changed assets just by opening them (a camera trigger's kind, a link's matrix)
    void Linked(PropertyNode listeningNode, PropertyNode changedNode)
    {
    }
}