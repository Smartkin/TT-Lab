using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Attributes;

public interface IGenericFieldChange<in TListener, in TLink> : IFieldChange where TListener : DocumentPartViewModel
    where TLink : DocumentPartViewModel
{
    void IFieldChange.DataChanged(DocumentPartViewModel listener, DocumentPartViewModel viewModel)
    {
        DataChanged((TListener)listener, (TLink)viewModel);
    }

    void DataChanged(TListener listener, TLink linkedViewModel);
}