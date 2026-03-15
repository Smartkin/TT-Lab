using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public class FlagsFieldViewModel : DocumentCompositeViewModel
{
    public override ReactiveCommand<Unit, Unit>? AddCommand => null;
    
    public FlagsFieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies) : base(document, data, dependencies)
    {
    }

    protected override void OnExpanded(CompositeDisposable disposables)
    {
        var enumValues = Property.Children.Select(x => new BoolFieldViewModel(Document, x, this)
        {
            Caption = Enum.GetName(Property.PropertyType, x.GetValue()!)!,
        });

        foreach (var enumValue in enumValues)
        {
            AddNode(enumValue);
        }
    }
}