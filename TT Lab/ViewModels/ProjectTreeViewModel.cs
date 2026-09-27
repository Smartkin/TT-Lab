using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.ViewModels;

public class ProjectTreeViewModel : Document, IHandle<ProjectManagerMessage>
{
    private readonly ProjectManager _projectManager;

    public ProjectTreeViewModel(ProjectManager projectManager, IEventAggregator eventAggregator)
    {
        _projectManager = projectManager;
        Id = "ProjectTree";
        Title = "Project Tree";
        eventAggregator.SubscribeOnUIThread(this);
    }

    public BindableCollection<ResourceTreeElementViewModel> ProjectTree => _projectManager.ProjectTree;

    public string SearchAsset
    {
        get => _projectManager.SearchAsset;
        set => _projectManager.SearchAsset = value;
    }

    public Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        switch (message.PropertyName)
        {
            case nameof(ProjectManager.ProjectTree):
                this.RaisePropertyChanged(nameof(ProjectTree));
                break;
            case nameof(ProjectManager.SearchAsset):
                this.RaisePropertyChanged(nameof(SearchAsset));
                break;
        }

        return Task.CompletedTask;
    }
}
