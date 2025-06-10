using Caliburn.Micro;
using TT_Lab.Assets;

namespace TT_Lab.ViewModels.Interfaces
{
    public interface IEditorViewModel : IScreen
    {
        LabURI EditableResource { get; set; }

        void SaveChanges(bool force = false);
    }
}
