using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A camera spline's sample: where it is, and its W, a word of the game's (<see cref="SplineSampleWord"/>), as whether the sample is a
/// key and a key's offset along the curve and share of the way toward the target. The word's other bits stay as they are
/// </summary>
public partial class SplineSampleFieldViewModel : DocumentDataViewModel<Vector4>
{
    [Reactive]
    private bool _isKey;

    [Reactive]
    private string? _offsetText;

    [Reactive]
    private string? _shareText;

    private TextFieldViewModel? _x;
    private TextFieldViewModel? _y;
    private TextFieldViewModel? _z;
    private bool _isShowingWord;
    private WordPart _editedPart;

    private enum WordPart
    {
        None,
        Key,
        Offset,
        Share,
    }

    public SplineSampleFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        ShowWord();
    }

    // Components are made once shown, like a vector's
    public TextFieldViewModel X => _x ??= CreateComponent("X");
    public TextFieldViewModel Y => _y ??= CreateComponent("Y");
    public TextFieldViewModel Z => _z ??= CreateComponent("Z");

    private PropertyNode WordNode => Property.FindChild(".W")!;

    private UInt32 Word => SplineSampleWord.Of(WordNode.GetValue<Single>());

    private TextFieldViewModel CreateComponent(string name)
    {
        return new TextFieldViewModel(Document, Property.FindChild($".{name}")!, this) { Caption = name };
    }

    public static string Format(Single value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool TryParse(string? text, out Single value)
    {
        return Single.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && Single.IsFinite(value);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        var wordNode = WordNode;
        wordNode.Changed += ShowWord;
        Disposable.Create(() => wordNode.Changed -= ShowWord).DisposeWith(disposables);
        ShowWord();

        this.WhenAnyValue(x => x.IsKey)
            .Skip(1)
            .Where(_ => !_isShowingWord)
            .Subscribe(isKey => SetWord(SplineSampleWord.WithKey(Word, isKey), WordPart.Key))
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.OffsetText)
            .Skip(1)
            .Where(_ => !_isShowingWord)
            .Subscribe(text =>
            {
                if (TryParse(text, out var offset))
                {
                    SetWord(SplineSampleWord.WithOffset(Word, offset), WordPart.Offset);
                }
            })
            .DisposeWith(disposables);
        this.WhenAnyValue(x => x.ShareText)
            .Skip(1)
            .Where(_ => !_isShowingWord)
            .Subscribe(text =>
            {
                if (TryParse(text, out var share))
                {
                    SetWord(SplineSampleWord.WithShare(Word, share), WordPart.Share);
                }
            })
            .DisposeWith(disposables);
    }

    private void SetWord(UInt32 word, WordPart part)
    {
        word = SplineSampleWord.Stored(word);
        if (word == Word)
        {
            return;
        }

        // The word is one value of the graph, whose changes in a row are one step: typing in one part is, the parts are steps of their own
        if (part != _editedPart || part == WordPart.Key)
        {
            Document.History.CloseStep();
        }

        _editedPart = part;
        WordNode.SetValue(SplineSampleWord.ToW(word));
    }

    private void ShowWord()
    {
        _isShowingWord = true;
        try
        {
            var word = Word;
            IsKey = SplineSampleWord.IsKey(word);
            // Keeps what's being typed while it still means the value, "1" doesn't turn into "0.9995" under the caret
            if (!TryParse(OffsetText, out var offset) || SplineSampleWord.WithOffset(word, offset) != word)
            {
                OffsetText = Format(SplineSampleWord.Offset(word));
            }

            if (!TryParse(ShareText, out var share) || SplineSampleWord.WithShare(word, share) != word)
            {
                ShareText = Format(SplineSampleWord.Share(word));
            }
        }
        finally
        {
            _isShowingWord = false;
        }
    }

    protected override void OnCurrentValueChanged()
    {
        _x?.SetValueCommand.Execute(Property.FindChild(".X")!.GetValue());
        _y?.SetValueCommand.Execute(Property.FindChild(".Y")!.GetValue());
        _z?.SetValueCommand.Execute(Property.FindChild(".Z")!.GetValue());
        ShowWord();

        base.OnCurrentValueChanged();
    }
}
