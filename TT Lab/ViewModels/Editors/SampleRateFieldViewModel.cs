using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A sample rate the PS2's pitch stands for (the rate times 4096 over 48000, truncated)
/// </summary>
public sealed record SampleRateChoice(int Hertz, UInt16 Pitch, string Label);

/// <summary>
/// A sound's pitch picked from the rates sounds come in; a pitch that is none of them stays as its own choice until another is picked
/// </summary>
public partial class SampleRateFieldViewModel : DocumentDataViewModel<UInt16>
{
    public static readonly IReadOnlyList<int> KnownRates = [8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000];

    private static readonly IReadOnlyList<SampleRateChoice> KnownChoices = KnownRates.Select(Known).ToList();

    [Reactive]
    private SampleRateChoice? _selectedRate;

    // A new list whenever the choices change: the combo box keeps the list it got, one changed in place had it pick items it no
    // longer had
    private IReadOnlyList<SampleRateChoice> _rates = KnownChoices;
    private bool _isShowing;

    public SampleRateFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _selectedRate = Choose(CurrentValue);
    }

    public IReadOnlyList<SampleRateChoice> Rates => _rates;

    public static SampleRateChoice Known(int hertz)
    {
        return new SampleRateChoice(hertz, ITwinSound.PitchOf((UInt32)hertz), $"{hertz} Hz");
    }

    /// <summary>
    /// The rate a pitch stands for, the pitch's own choice when it's none of the known rates
    /// </summary>
    public static SampleRateChoice Custom(UInt16 pitch)
    {
        var hertz = (int)Math.Round(pitch * 48000.0 / 4096.0);
        return new SampleRateChoice(hertz, pitch, $"Pitch {pitch} (about {hertz} Hz)");
    }

    // A pitch of its own only stays listed while it's the value
    private SampleRateChoice Choose(UInt16 pitch)
    {
        var known = KnownChoices.FirstOrDefault(rate => rate.Pitch == pitch);
        var chosen = known ?? Custom(pitch);
        IReadOnlyList<SampleRateChoice> rates = known != null ? KnownChoices : [.. KnownChoices, chosen];
        if (!rates.SequenceEqual(_rates))
        {
            _rates = rates;
            this.RaisePropertyChanged(nameof(Rates));
        }

        return chosen;
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedRate)
            .Skip(1)
            .WhereNotNull()
            .Where(rate => !_isShowing && rate.Pitch != CurrentValue)
            .Subscribe(rate => SetCurrentValue(rate.Pitch))
            .DisposeWith(disposables);
    }

    // What the combo box does while the list and the selection change isn't a pick
    protected override void OnCurrentValueChanged()
    {
        _isShowing = true;
        try
        {
            var chosen = Choose(CurrentValue);
            if (SelectedRate?.Pitch != CurrentValue)
            {
                SelectedRate = chosen;
            }
        }
        finally
        {
            _isShowing = false;
        }
    }
}
