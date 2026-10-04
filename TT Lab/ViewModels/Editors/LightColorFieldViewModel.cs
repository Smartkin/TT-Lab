using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A scenery light's color picked as a color. The game's lights keep colors whose red, green and blue add up to 1 and their brightness
/// in the intensity, so the picker shows the light's hue at full brightness and a picked color is stored adding up to what the light's
/// did (1 when it had none), W as it was
/// </summary>
public partial class LightColorFieldViewModel : DocumentDataViewModel<Vector4>
{
    [Reactive]
    private Color _shownColor;

    private bool _isShowingValue;

    public LightColorFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _shownColor = ToShown(CurrentValue);
    }

    /// <summary>
    /// The color as the game has it
    /// </summary>
    public string StoredText => CurrentValue is { } color ? $"{color.X:0.###}, {color.Y:0.###}, {color.Z:0.###}" : string.Empty;

    public static Color ToShown(Vector4? color)
    {
        var brightest = color == null ? 0.0f : MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (!(brightest > 0.0f))
        {
            return Colors.Black;
        }

        Byte Channel(Single value) => (Byte)Math.Clamp(MathF.Round(value / brightest * 255.0f), 0.0f, 255.0f);
        return Color.FromRgb(Channel(color!.X), Channel(color.Y), Channel(color.Z));
    }

    public static Vector4 FromPicked(Color picked, Vector4? current)
    {
        var total = current == null ? 0.0f : current.X + current.Y + current.Z;
        if (!(total > 0.0f))
        {
            total = 1.0f;
        }

        var sum = picked.R + picked.G + picked.B;
        var w = current?.W ?? 0.0f;
        return sum == 0 ? new Vector4(0, 0, 0, w) : new Vector4(picked.R * total / sum, picked.G * total / sum, picked.B * total / sum, w);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.ShownColor)
            .Skip(1)
            .Where(color => !_isShowingValue && color != ToShown(CurrentValue))
            .Subscribe(color => SetCurrentValue(FromPicked(color, CurrentValue)))
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        _isShowingValue = true;
        try
        {
            ShownColor = ToShown(CurrentValue);
            this.RaisePropertyChanged(nameof(StoredText));
        }
        finally
        {
            _isShowingValue = false;
        }
    }
}
