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

namespace TT_Lab.ViewModels.Editors;

public partial class EnumFieldViewModel : DocumentDataViewModel<object>
{
    [Reactive]
    private object? _selectedValue;

    public ReadOnlyObservableCollection<object> EnumValues;

    public EnumFieldViewModel(DocumentViewModel document, object data) : base(document, data)
    {
        _selectedValue = data;
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        if (EditorParameters.TryGetValue(EnumTypeName, out var enumType))
        {
            _enumType = (Type)enumType;
        }

        var enumValues = Enum.GetValues(_enumType).Cast<object>().ToArray();
        EnumValues = new ReadOnlyObservableCollection<Object>(new ObservableCollection<Object>(enumValues));

        this.WhenAnyValue(x => x.SelectedValue)
            .Where(value => value is not null && MiscUtils.ConvertEnum(_enumType, value) != null)
            .Subscribe(selection =>
            {
                Data = MiscUtils.ConvertEnum(_enumType, selection)!;
            }).DisposeWith(disposables);
    }
    
    public const string EnumTypeName = "ENUM_FIELD_ENUM_TYPE_NAME";

    private Type _enumType = typeof(DummyEnum);

    private enum DummyEnum
    {
        DEVELOPER_FORGOR_TO_PROVIDE_ENUM_TYPE_IN_EDITOR_PARAMETERS
    }
}