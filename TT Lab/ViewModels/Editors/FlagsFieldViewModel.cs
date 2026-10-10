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
        // A composite shows its caption in a header, the flags' view has none: the row shows it like a field's
        IsCaptionVisible = true;
    }

    protected override void OnExpanded(CompositeDisposable disposables)
    {
        var enumValues = Property.Children.Where(x => !EnumCaptions.IsNeverRead(Property.PropertyType, x.Name)).Select(x => new BoolFieldViewModel(Document, x, this)
        {
            Caption = EnumCaptions.Of(Property.PropertyType, x.Name),
            Hint = EnumCaptions.HintOf(Property.PropertyType, x.Name),
        });

        foreach (var enumValue in enumValues)
        {
            AddNode(enumValue);
        }
    }
}