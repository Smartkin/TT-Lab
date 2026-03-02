using System;
using System.Collections.ObjectModel;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using Dock.Model.ReactiveUI.Controls;
using Splat;

namespace TT_Lab.ViewModels;

public class EditorsViewModel : Document
{
    private readonly ScenesEditorsViewModel _scenesEditorsViewModel;
    private readonly ResourcesEditorsViewModel _resourcesEditorsViewModel;
    
    public ObservableCollection<EditorsViewerViewModel> Editors { get; }

    public EditorsViewModel(ScenesEditorsViewModel scenesEditorsViewModel,
        ResourcesEditorsViewModel resourcesEditorsViewModel)
    {
        _scenesEditorsViewModel = scenesEditorsViewModel;
        _resourcesEditorsViewModel = resourcesEditorsViewModel;

        Editors = [ScenesEditorsViewModel, ResourcesEditorsViewModel];
    }

    public void Save()
    {
        _scenesEditorsViewModel.Save();
        _resourcesEditorsViewModel.Save();
    }

    public ScenesEditorsViewModel ScenesEditorsViewModel => _scenesEditorsViewModel;
    public ResourcesEditorsViewModel ResourcesEditorsViewModel => _resourcesEditorsViewModel;
}