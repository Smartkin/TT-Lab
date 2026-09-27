using Avalonia.Media;
using Avalonia.Media.Immutable;
using ReactiveUI;
using ReactiveUI.Validation.Abstractions;
using ReactiveUI.Validation.Contexts;

namespace TT_Lab.ViewModels.Editors;

// Documents easily have thousands of editors, the properties derived from others are computed instead of being observed and validation
// is only set up by the editors that validate something. Creating the editors of a long list used to freeze the editor for seconds
public abstract class DocumentBaseViewModel : ReactiveObject, IValidatableViewModel
{
    private bool _isVisible = true;
    private int _depth;
    private bool _isReadOnly;
    private ValidationContext? _validationContext;

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

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public int Depth
    {
        get => _depth;
        set
        {
            if (_depth == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _depth, value);
            this.RaisePropertyChanged(nameof(DepthDependentBrush));
        }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (_isReadOnly == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _isReadOnly, value);
            this.RaisePropertyChanged(nameof(CanWrite));
        }
    }

    public bool CanWrite => !IsReadOnly;

    public IBrush DepthDependentBrush => DocumentBrushes[Depth % DocumentBrushes.Length];

    public IValidationContext ValidationContext => _validationContext ??= new ValidationContext();

    protected bool HasValidationContext => _validationContext != null;
}
