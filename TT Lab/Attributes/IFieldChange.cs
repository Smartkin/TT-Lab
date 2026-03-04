using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes;

public interface IFieldChange
{
    void DataChanged(DocumentPartViewModel listeningViewModel, DocumentPartViewModel changedViewModel);
}