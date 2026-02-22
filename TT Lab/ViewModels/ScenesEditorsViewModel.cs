using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
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
        Dispatcher.UIThread.Post(() =>
        {
            var item = Items.FirstOrDefault(tab => tab!.EditableResource == message.Asset.URI, null);
            if (item != null)
            {
                ActivateItemAsync(item, cancellationToken);
                return;
            }

            var newEditor = new TabbedEditorViewModel(message.Asset)
            {
                DisplayName = message.Asset.Name
            };
            
            newEditor.Deactivated += async (sender, args) =>
            {
                if (args.WasClosed)
                {
                    Items.Remove(newEditor);
                }
                
                await Task.CompletedTask;
            };

            ActivateItemAsync(newEditor, cancellationToken);
        });
        return Task.CompletedTask;
    }
}