using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using TT_Lab.Util;

namespace TT_Lab.ViewModels.Editors;

public class FlagsFieldViewModel : DocumentDataViewModel<object>
{
    public ReadOnlyObservableCollection<BoolFieldViewModel> BoolFields;
    
    public FlagsFieldViewModel(DocumentViewModel document, object data) : base(document, data)
    {
    }

    public override Object GetFinalData()
    {
        var resultValue = 0U;
        var setShift = 0;
        foreach (var field in BoolFields)
        {
            var fieldValue = field.Data;
            var setValue = (fieldValue ? 1U : 0U) << setShift;
            resultValue |= setValue;
            setShift += 1;
        }
        
        return Enum.ToObject(_enumType, resultValue);
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        Debug.Assert(Data.GetType().IsEnum, "Provided data is not an enum!");
        if (EditorParameters.TryGetValue(EnumTypeName, out var enumType))
        {
            _enumType = (Type)enumType;
        }
        else
        {
            _enumType = Data.GetType();
        }

        var enumValues = Enum.GetValues(_enumType).Cast<object>().Select(x => new BoolFieldViewModel(Document, ((Enum)Data).HasFlag((Enum)x))
        {
            Caption = Enum.GetName(_enumType, x)!,
        });
        BoolFields = new ReadOnlyObservableCollection<BoolFieldViewModel>(new ObservableCollection<BoolFieldViewModel>(enumValues));
    }
    
    public const string EnumTypeName = "ENUM_FIELD_ENUM_TYPE_NAME";

    private Type _enumType;
}