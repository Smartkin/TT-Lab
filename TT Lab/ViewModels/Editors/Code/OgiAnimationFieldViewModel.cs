using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Code;

public record OgiAnimationChoice(UInt16 Id, string Name, bool IsMissing = false)
{
    public string IdText => Id == ModelSlot.NoAnimation ? string.Empty : $"{Id:X}";

    // A word of the search is in the name or in the ID, written in hex with or without 0x
    public bool Matches(string word)
    {
        if (Name.Contains(word, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var hex = word.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? word[2..] : word;
        return hex.Length != 0 && IdText.Contains(hex, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Picks one of the animations of the OGI in the same model slot
/// </summary>
public partial class OgiAnimationFieldViewModel : DocumentDataViewModel<UInt16>
{
    private static readonly OgiAnimationChoice NoAnimation = new(ModelSlot.NoAnimation, "None");

    [Reactive]
    private OgiAnimationChoice? _selectedAnimation;

    [Reactive]
    private IReadOnlyList<OgiAnimationChoice> _animations = [];

    // The dropdown's search: the animations with every word typed in their name or ID, and the one Enter picks
    [Reactive]
    private string _search = string.Empty;

    [Reactive]
    private IReadOnlyList<OgiAnimationChoice> _shownAnimations = [];

    [Reactive]
    private OgiAnimationChoice? _highlighted;

    public OgiAnimationFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }

    public bool CanChooseAnimation => Animations.Count > 1;

    private PropertyNode? OgiNode => Property.Parent?.Children.FirstOrDefault(child => child.Name == nameof(ModelSlot.Ogi));

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        LoadChoices();
        var ogiNode = OgiNode;
        if (ogiNode != null)
        {
            ogiNode.Changed += LoadChoices;
            Disposable.Create(() => ogiNode.Changed -= LoadChoices).DisposeWith(disposables);
        }

        this.WhenAnyValue(x => x.SelectedAnimation)
            .Skip(1)
            .WhereNotNull()
            .Where(animation => !_isShowing && animation.Id != CurrentValue)
            .Subscribe(animation => SetCurrentValue(animation.Id))
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.Search)
            .Skip(1)
            .Subscribe(_ => ShowMatches(keepHighlight: false))
            .DisposeWith(disposables);
    }

    // Set while the choices and the selection show the value: the combo box handed its old selection back as the list changed, which
    // became the value again, and the two went back and forth until the stack ran out
    private bool _isShowing;

    protected override void OnCurrentValueChanged()
    {
        LoadChoices();
    }

    // Opening the dropdown shows every animation with the slot's highlighted
    public void StartSearch()
    {
        Search = string.Empty;
        ShowMatches(keepHighlight: false);
        if (SelectedAnimation != null && ShownAnimations.Contains(SelectedAnimation))
        {
            Highlighted = SelectedAnimation;
        }
    }

    public void MoveHighlight(int step)
    {
        if (ShownAnimations.Count == 0)
        {
            return;
        }

        var index = Highlighted == null ? -1 : ShownAnimations.ToList().IndexOf(Highlighted);
        if (index < 0)
        {
            index = step > 0 ? 0 : ShownAnimations.Count - 1;
        }
        else
        {
            index = Math.Clamp(index + step, 0, ShownAnimations.Count - 1);
        }

        Highlighted = ShownAnimations[index];
    }

    // False when the search shows nothing to pick
    public bool PickHighlighted()
    {
        if (Highlighted == null)
        {
            return false;
        }

        Pick(Highlighted);
        return true;
    }

    public void Pick(OgiAnimationChoice animation)
    {
        SelectedAnimation = animation;
    }

    // Typing highlights the first animation shown, Enter picks it. The list goes first: the list box hands back no selection as its items change
    private void ShowMatches(bool keepHighlight)
    {
        // A text box's text can be null
        var words = (Search ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var shown = Animations.Where(animation => words.All(animation.Matches)).ToList();
        var highlight = keepHighlight && Highlighted != null && shown.Contains(Highlighted) ? Highlighted : shown.FirstOrDefault();
        if (!shown.SequenceEqual(ShownAnimations))
        {
            ShownAnimations = shown;
        }

        Highlighted = highlight;
    }

    // The slot's model can change any time, the animations are the ones of the model it has
    private void LoadChoices()
    {
        var choices = new List<OgiAnimationChoice> { NoAnimation };
        var ogi = (Property.Target as ModelSlot)?.Ogi ?? LabURI.Empty;
        var assetManager = AssetManager.Get();
        if (ogi != LabURI.Empty && assetManager.DoesAssetExist(ogi))
        {
            choices.AddRange(assetManager.GetAssetData<OGIData>(ogi).Animations
                .Where(animation => animation.ID < ModelSlot.NoAnimation)
                .Select(animation => new OgiAnimationChoice((UInt16)animation.ID, animation.Name)));
        }

        var current = choices.FirstOrDefault(choice => choice.Id == CurrentValue);
        // An animation the model doesn't have gets built as none, it's kept to be seen until another one is picked
        if (current == null)
        {
            current = new OgiAnimationChoice(CurrentValue, "Not in the OGI", true);
            choices.Add(current);
        }

        _isShowing = true;
        try
        {
            if (!choices.SequenceEqual(Animations))
            {
                Animations = choices;
            }

            SelectedAnimation = current;
        }
        finally
        {
            _isShowing = false;
        }

        ShowMatches(keepHighlight: true);
        this.RaisePropertyChanged(nameof(CanChooseAnimation));
    }
}
