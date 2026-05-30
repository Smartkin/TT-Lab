using System;
using System.IO;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using Caliburn.Micro;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Project;
using TT_Lab.Project.Messages;
using TT_Lab.Util;

namespace TT_Lab.ViewModels;

public partial class LogViewModel : Document, IActivatableViewModel, IHandle<ProjectManagerMessage>
{
    private readonly ProjectManager _projectManager;

    [Reactive]
    private TextDocument _text = new();

    [Reactive]
    private int _caretOffset;

    public LogViewModel(IEventAggregator eventAggregator, ProjectManager projectManager)
    {
        _projectManager = projectManager;
        this.WhenActivated((CompositeDisposable disposables) => { });
        eventAggregator.SubscribeOnUIThread(this);
    }

    public void Clear()
    {
        Text.Text = "";
    }

    public ViewModelActivator Activator { get; } = new();
    
    public Stream SadEasterEgg => new FileStream(ManifestResourceLoader.GetPathInExe("Images/SadTransparent.gif"), FileMode.Open, FileAccess.Read);
    
    public Boolean SadEasterEggVisibility => Preferences.GetPreference<Boolean>(Preferences.SillinessEnabled) && _projectManager.IsCreatingProject;
    
    public async Task HandleAsync(ProjectManagerMessage message, CancellationToken cancellationToken)
    {
        if (message.PropertyName != nameof(ProjectManager.IsCreatingProject))
        {
            return;
        }
        
        this.RaisePropertyChanged(nameof(SadEasterEggVisibility));

        await Task.CompletedTask;
    }
}