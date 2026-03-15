using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class EnumFieldViewModel : DocumentDataViewModel<object>
{
    [Reactive]
    private object? _selectedValue;

    public ReadOnlyObservableCollection<object> EnumValues;

    public EnumFieldViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies) : base(document, data, dependencies)
    {
        _selectedValue = data;
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        var enumValues = Enum.GetValues(Property.PropertyType).Cast<object>().ToArray();
        EnumValues = new ReadOnlyObservableCollection<Object>(new ObservableCollection<Object>(enumValues));
    }

    protected override void SetCurrentValue(Object? value)
    {
        if (value is not null && MiscUtils.ConvertEnum(Property.PropertyType, value) != null)
        {
            base.SetCurrentValue(value);
        }
    }
}