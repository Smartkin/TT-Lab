using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector2FieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<Vector2>(document, data, dependencies)
{
    private TextFieldViewModel? _x;
    private TextFieldViewModel? _y;

    // Components are made once shown, a long list of vectors mostly consists of editors that never are
    public TextFieldViewModel X => _x ??= CreateComponent("X");
    public TextFieldViewModel Y => _y ??= CreateComponent("Y");

    private TextFieldViewModel CreateComponent(string name)
    {
        return new TextFieldViewModel(Document, Property.Find(name)!, this) { Caption = name };
    }

    protected override void OnCurrentValueChanged()
    {
        _x?.SetValueCommand.Execute(Property.Find("X")!.GetValue());
        _y?.SetValueCommand.Execute(Property.Find("Y")!.GetValue());
        
        base.OnCurrentValueChanged();
    }
}