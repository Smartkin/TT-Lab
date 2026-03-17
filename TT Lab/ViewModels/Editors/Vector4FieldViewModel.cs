using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Vector4FieldViewModel : DocumentDataViewModel<Vector4>
{
    public TextFieldViewModel X { get; }
    public TextFieldViewModel Y { get; }
    public TextFieldViewModel Z { get; }
    public TextFieldViewModel W { get; }

    public Vector4FieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
        : base(document, data, dependencies)
    {
        X = new TextFieldViewModel(document, Property.Find("X")!, this) { Caption = "X" };
        Y = new TextFieldViewModel(document, Property.Find("Y")!, this) { Caption = "Y" };
        Z = new TextFieldViewModel(document, Property.Find("Z")!, this) { Caption = "Z" };
        W = new TextFieldViewModel(document, Property.Find("W")!, this) { Caption = "W" };
    }

    protected override void NodeOnChanged()
    {
        X.SetValueCommand.Execute(Property.Find("X")!.GetValue());
        Y.SetValueCommand.Execute(Property.Find("Y")!.GetValue());
        Z.SetValueCommand.Execute(Property.Find("Z")!.GetValue());
        W.SetValueCommand.Execute(Property.Find("W")!.GetValue());
        
        base.NodeOnChanged();
    }
}