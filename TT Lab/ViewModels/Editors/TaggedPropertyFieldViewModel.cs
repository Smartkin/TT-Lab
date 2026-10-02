using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A tagged value typed as text: an int as it is, Float(x), Angle(x) in degrees, Prop(index) or Raw(0x...). A plain literal keeps the
/// value's type, Int(x), Float(x) and Angle(x) change it
/// </summary>
public partial class TaggedPropertyFieldViewModel : DocumentDataViewModel<object>
{
    [Reactive]
    private string? _text;

    private bool _isShowingValue;

    public TaggedPropertyFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _text = TaggedValue.Describe(Bits);
    }

    private UInt32 Bits => CurrentValue is TaggedProperty property ? property.Bits : 0;

    private bool TryParse(string? text, out UInt32 bits)
    {
        bits = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            bits = TaggedValue.ParseKeepingType(text, Bits);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            return false;
        }
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.Text)
            .Skip(1)
            .Where(_ => !_isShowingValue)
            .Subscribe(text =>
            {
                if (TryParse(text, out var bits) && bits != Bits)
                {
                    SetCurrentValue(new TaggedProperty(bits));
                }
            })
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        _isShowingValue = true;
        try
        {
            // Keeps what's being typed while it still means the value
            if (!TryParse(Text, out var typed) || typed != Bits)
            {
                Text = TaggedValue.Describe(Bits);
            }
        }
        finally
        {
            _isShowingValue = false;
        }
    }
}
