using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

public sealed class ScenesEditorsViewModel : EditorsViewerViewModel, IHandle<CreateEditorMessage<ChunkEditorViewModel>>
{
    private readonly IEventAggregator _eventAggregator;
    private readonly ProjectManager _projectManager;

    public ScenesEditorsViewModel(IEventAggregator eventAggregator, ProjectManager projectManager)
    {
        DisplayName = "Scenes Editors";
        _projectManager = projectManager;
        _eventAggregator = eventAggregator;
        _eventAggregator.SubscribeOnUIThread(this);
    }

    public Task HandleAsync(CreateEditorMessage<ChunkEditorViewModel> message, CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(() =>
            {
                var item = Items.FirstOrDefault(tab => tab!.EditableResource == message.ResourceURI, null);
                if (item != null)
                {
                    ActivateItemAsync(item, cancellationToken);
                    return;
                }

                var assetManager = _projectManager.OpenedProject!.AssetManager;
                var newEditor = new TabbedEditorViewModel(message.ResourceURI, message.EditorType)
                {
                    DisplayName = assetManager.GetAsset(message.ResourceURI).Name
                };
                
                newEditor.Deactivated += async (sender, args) =>
                {
                    if (args.WasClosed)
                    {
                        Items.Remove(newEditor);
                    }
                };

                ActivateItemAsync(newEditor, cancellationToken);
            },
            cancellationToken);
    }
}