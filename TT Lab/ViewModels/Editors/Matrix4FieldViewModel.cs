using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public class Matrix4FieldViewModel : DocumentDataViewModel<Matrix4>
{
    public Vector4FieldViewModel V1 { get; }
    public Vector4FieldViewModel V2 { get; }
    public Vector4FieldViewModel V3 { get; }
    public Vector4FieldViewModel V4 { get; }

    public Matrix4FieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
        : base(document, data, dependencies)
    {
        V1 = new Vector4FieldViewModel(document, Property.Find("Column1")!, this);
        V2 = new Vector4FieldViewModel(document, Property.Find("Column2")!, this);
        V3 = new Vector4FieldViewModel(document, Property.Find("Column3")!, this);
        V4 = new Vector4FieldViewModel(document, Property.Find("Column4")!, this);
    }
}