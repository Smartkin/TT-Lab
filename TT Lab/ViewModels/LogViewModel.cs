using System;
using System.IO;
using System.Reactive.Disposables;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Util;

namespace TT_Lab.ViewModels;

public partial class LogViewModel : Document, IActivatableViewModel
{
    [Reactive]
    private string _text = string.Empty;

    [Reactive]
    private int _linesAmount;

    public LogViewModel()
    {
        this.WhenActivated((CompositeDisposable disposables) => { });
    }

    public void Clear()
    {
        Text = string.Empty;
    }

    public ViewModelActivator Activator { get; } = new();
    
    public Stream SadEasterEgg => new FileStream(ManifestResourceLoader.GetPathInExe("Images/SadTransparent.gif"), FileMode.Open, FileAccess.Read);
    
    public Boolean SadEasterEggVisibility => Preferences.GetPreference<Boolean>(Preferences.SillinessEnabled);
}