using System;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;

namespace TT_Lab.ViewModels.Editors;

public abstract partial class DocumentBaseViewModel : ReactiveValidationObject
{
    [ObservableAsProperty]
    private bool _canWrite;

    [ObservableAsProperty]
    private IBrush _depthDependentBrush;
    
    [Reactive]
    private bool _isVisible = true;

    [Reactive]
    private int _depth;
    
    [Reactive]
    private bool _isReadOnly;
    
    private static readonly IBrush[] DocumentBrushes;

    static DocumentBaseViewModel()
    {
        DocumentBrushes =
        [
            new ImmutableSolidColorBrush(Color.FromRgb(0, 0, 0)),
            new ImmutableSolidColorBrush(Color.FromRgb(25, 25, 25)),
            new ImmutableSolidColorBrush(Color.FromRgb(50, 50, 50)),
            new ImmutableSolidColorBrush(Color.FromRgb(75, 75, 75)),
            new ImmutableSolidColorBrush(Color.FromRgb(50, 50, 50)),
            new ImmutableSolidColorBrush(Color.FromRgb(25, 25, 25)),
        ];
    }

    protected DocumentBaseViewModel()
    {
        _canWriteHelper = this.WhenAnyValue(x => x.IsReadOnly)
            .Select(x => !x)
            .ToProperty(this, x => x.CanWrite);

        _depthDependentBrushHelper = this.WhenAnyValue(x => x.Depth)
            .Select(x => DocumentBrushes[x % DocumentBrushes.Length])
            .ToProperty(this, x => x.DepthDependentBrush);
    }
}