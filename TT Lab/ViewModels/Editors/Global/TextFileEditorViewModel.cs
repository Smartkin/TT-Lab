using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Global;

/// <summary>
/// A font the text can be shown with, none shows it as text
/// </summary>
public sealed record TextFileFont(string Name, Font? Font);

public sealed record TextFileGlyph(char Character, Bitmap? Image, string Tip);

/// <summary>
/// The game's texts shown with one of its fonts, which draw some of the characters as controller buttons
/// </summary>
public partial class TextFileEditorViewModel(DocumentViewModel document, PropertyNode text, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<string>(document, text, dependencies)
{
    private IReadOnlyList<TextFileFont>? _fonts;

    [Reactive]
    private string? _text;

    [Reactive]
    private TextFileFont? _selectedFont;

    [Reactive(SetModifier = AccessModifier.Private)]
    private PsfGlyphs? _glyphs;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<TextFileGlyph> _buttons = [];

    public IReadOnlyList<TextFileFont> Fonts => _fonts ??= LoadFonts();

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        RxSchedulers.MainThreadScheduler.Schedule(this, (_, viewModel) =>
        {
            Text = CurrentValue ?? string.Empty;
            return viewModel.WhenAnyValue(x => x.Text).ObserveOn(RxSchedulers.MainThreadScheduler)
                .Skip(1)
                .Subscribe(value => SetValueCommand.Execute(value));
        }).DisposeWith(disposables);

        SelectedFont ??= GetDefaultFont();
        this.WhenAnyValue(x => x.SelectedFont).Subscribe(UseFont).DisposeWith(disposables);
    }

    private IReadOnlyList<TextFileFont> LoadFonts()
    {
        var fonts = new List<TextFileFont> { new("Plain text", null) };
        if ((Property.Target as AbstractAssetData)?.GetOwner() is { } owner)
        {
            fonts.AddRange(AssetManager.Get().GetRelatedAssetsOf<Font>(owner.Package).OrderBy(font => font.Alias).Select(font => new TextFileFont(font.Alias, font)));
        }

        return fonts;
    }

    private TextFileFont GetDefaultFont()
    {
        var font = (Property.Target as AbstractAssetData)?.GetOwner() is { } owner ? PsfGlyphs.DefaultFontOf(owner.Package) : null;
        return Fonts.FirstOrDefault(item => item.Font != null && item.Font == font) ?? Fonts[0];
    }

    private void UseFont(TextFileFont? font)
    {
        PsfGlyphs? glyphs = null;
        if (font?.Font != null)
        {
            try
            {
                glyphs = PsfGlyphs.FromFont(((IAsset)font.Font).GetData<FontData>());
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Couldn't read the glyphs of {font.Name}: {ex.Message}", Log.LogType.Warning);
            }
        }

        Glyphs = glyphs;
        // The controller buttons first, then every other symbol the font draws: what a keyboard has no key for
        Buttons = glyphs == null
            ? []
            : PsfGlyphs.ButtonCharacters
                .Concat(glyphs.Characters.Where(character => !PsfGlyphs.ButtonCharacters.Contains(character) && !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character)))
                .Select(character => glyphs.TryGetGlyph(character, out var glyph) ? glyph : null)
                .OfType<PsfGlyphs.Glyph>()
                .Where(glyph => glyph.Image != null)
                .Select(glyph => new TextFileGlyph(glyph.Character, glyph.Image, $"Inserts {glyph.Character} (0x{(int)glyph.Character:X2})"))
                .ToList();
    }

    // The document's history and other editors of the value change it from outside the editor
    protected override void OnCurrentValueChanged()
    {
        Text = CurrentValue ?? string.Empty;
    }

    // Typing on keeps changing the same step of the document's history, the editor ends it when the caret goes elsewhere
    public void EndTypingStep() => Document.History.CloseStep();
}
