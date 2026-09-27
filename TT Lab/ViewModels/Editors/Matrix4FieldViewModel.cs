using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Matrix4FieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<Matrix4>(document, data, dependencies)
{
    private Vector4FieldViewModel? _v1;
    private Vector4FieldViewModel? _v2;
    private Vector4FieldViewModel? _v3;
    private Vector4FieldViewModel? _v4;

    // Columns are made once shown, like the components of vectors
    public Vector4FieldViewModel V1 => _v1 ??= CreateColumn("Column1");
    public Vector4FieldViewModel V2 => _v2 ??= CreateColumn("Column2");
    public Vector4FieldViewModel V3 => _v3 ??= CreateColumn("Column3");
    public Vector4FieldViewModel V4 => _v4 ??= CreateColumn("Column4");

    private Vector4FieldViewModel CreateColumn(string name)
    {
        return new Vector4FieldViewModel(Document, Property.Find(name)!, this);
    }

    protected override void OnCurrentValueChanged()
    {
        _v1?.SetValueCommand.Execute(Property.Find("Column1")!.GetValue<Vector4>());
        _v2?.SetValueCommand.Execute(Property.Find("Column2")!.GetValue<Vector4>());
        _v3?.SetValueCommand.Execute(Property.Find("Column3")!.GetValue<Vector4>());
        _v4?.SetValueCommand.Execute(Property.Find("Column4")!.GetValue<Vector4>());
        
        base.OnCurrentValueChanged();
    }
}