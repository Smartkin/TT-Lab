using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using TT_Lab.Util;

namespace TT_Lab.ViewModels.Editors;

public partial class TextFieldViewModel(DocumentViewModel document, object data) : DocumentDataViewModel<object>(document, data)
{
    [Reactive]
    private string? _text = data.ToString();

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        if (EditorParameters.TryGetValue(TextFieldConverter, out var value))
        {
            _converterType = (Type)value;
            _converter = (IStringConverter)System.Activator.CreateInstance(_converterType)!;
        }

        this.ValidationRule(viewModel => viewModel.Text, text => !string.IsNullOrWhiteSpace(text),
            $"{Caption} must not be empty!").DisposeWith(disposables);
        this.ValidationRule(viewModel => viewModel.Text, text => _converter == null || _converter.IsConvertible(text ?? string.Empty),
            $"{Caption} must be convertible!").DisposeWith(disposables);

        this.WhenAnyValue(x => x.Text)
            .Where(s => !string.IsNullOrEmpty(s) && (_converter == null || _converter.IsConvertible(s)))
            .Subscribe(s =>
            {
                if (_converter != null)
                {
                    Data = _converter.ConvertFromString(s!);
                    return;
                }
                
                Data = s!;
            }).DisposeWith(disposables);
    }

    public const string TextFieldConverter = "TEXT_FIELD_CONVERTER_TYPE_NAME";

    private Type? _converterType;
    private IStringConverter? _converter;
    
}