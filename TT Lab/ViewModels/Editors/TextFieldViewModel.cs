using System;
using System.Collections;
using System.Collections.Generic;
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

    private static readonly Dictionary<Type, IStringConverter> DefaultStringConverters = new();

    private bool _canClose = true;

    static TextFieldViewModel()
    {
        DefaultStringConverters[typeof(Byte)] = new ByteConverter();
        DefaultStringConverters[typeof(SByte)] = new SByteConverter();
        DefaultStringConverters[typeof(UInt16)] = new UInt16Converter();
        DefaultStringConverters[typeof(Int16)] = new Int16Converter();
        DefaultStringConverters[typeof(UInt32)] = new UInt32Converter();
        DefaultStringConverters[typeof(Int32)] = new Int32Converter();
        DefaultStringConverters[typeof(UInt64)] = new UInt64Converter();
        DefaultStringConverters[typeof(Int64)] = new Int64Converter();
        DefaultStringConverters[typeof(UInt128)] = new UInt128Converter();
        DefaultStringConverters[typeof(Int128)] = new Int128Converter();
        DefaultStringConverters[typeof(Single)] = new SingleConverter();
        DefaultStringConverters[typeof(Double)] = new DoubleConverter();
        DefaultStringConverters[typeof(Decimal)] = new DecimalConverter();
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        if (EditorParameters.TryGetValue(TextFieldConverter, out var value))
        {
            _converter = (IStringConverter)System.Activator.CreateInstance((Type)value)!;
        }
        else
        {
            _converter = DetermineConverter(Data.GetType());
        }

        _stringLength = GetEditorParameter(TextFieldStringLength, UInt32.MaxValue);

        if (EditorParameters.TryGetValue(TextFieldNumberRange, out var range))
        {
            var numRange = (IEnumerable)range;
            var idx = 0;
            foreach (var num in numRange)
            {
                _numberRange[idx++] = Convert.ToDouble(num);
            }
        }

        this.ValidationRule(viewModel => viewModel.Text, text => !string.IsNullOrWhiteSpace(text),
            $"{Caption} must not be empty!").DisposeWith(disposables);
        this.ValidationRule(viewModel => viewModel.Text, text => _converter == null || _converter.IsConvertible(text ?? string.Empty),
            $"{Caption} must be convertible!").DisposeWith(disposables);
        this.ValidationRule(viewModel => viewModel.Text, CheckNumberRange,
            $"{Caption} must be in the range {_numberRange[0]}-{_numberRange[1]}!").DisposeWith(disposables);
        this.ValidationRule(viewModel => viewModel.Text, text => (text?.Length ?? 0) <= _stringLength,
            $"{Caption} must be less than {_stringLength} long!").DisposeWith(disposables);

        this.IsValid().Subscribe(x =>
        {
            _canClose = x;
        }).DisposeWith(disposables);
        
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

    public override Boolean CanClose()
    {
        return _canClose;
    }

    private bool CheckNumberRange(string? s)
    {
        if (string.IsNullOrEmpty(s) || _converter == null || !_converter.IsConvertible(s))
        {
            return true;
        }

        if (_converter.ConvertFromString(s) is not IComparable number)
        {
            return true;
        }

        dynamic compareToNum = number;
        return _numberRange[0] <= compareToNum && _numberRange[1] >= compareToNum;
    }

    private static IStringConverter? DetermineConverter(Type type)
    {
        return DefaultStringConverters.TryGetValue(type, out var converter) ? converter : null;
    }

    public const string TextFieldConverter = "TEXT_FIELD_CONVERTER_TYPE_NAME";
    public const string TextFieldStringLength = "TEXT_FIELD_STRING_LENGTH";
    public const string TextFieldNumberRange = "TEXT_FIELD_NUMBER_RANGE";

    private uint _stringLength = UInt32.MaxValue;
    private IStringConverter? _converter;
    private readonly double[] _numberRange = [double.MinValue, double.MaxValue];
}