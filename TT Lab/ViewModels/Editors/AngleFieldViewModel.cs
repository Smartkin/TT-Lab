using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// An angle the game keeps in 65536ths of a turn (cameras' pitch and yaw, emitters' tilt and yaw), edited in degrees. The value stays
/// the integer type the game has, unsigned ones wrap around a turn
/// </summary>
public partial class AngleFieldViewModel : DocumentDataViewModel<object>
{
    public const double UnitsPerTurn = 65536.0;

    [Reactive]
    private string? _degreesText;

    private bool _isShowingValue;

    public AngleFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _degreesText = Format(ToDegrees(CurrentValue));
    }

    public static double ToDegrees(object? units)
    {
        return units == null ? 0.0 : Convert.ToDouble(units, CultureInfo.InvariantCulture) * 360.0 / UnitsPerTurn;
    }

    /// <summary>
    /// The degrees as the property's integer type: unsigned types take the turn's remainder, signed ones half a turn either way
    /// </summary>
    public static object ToUnits(double degrees, Type type)
    {
        var units = (long)Math.Round(degrees / 360.0 * UnitsPerTurn);
        var turn = (long)UnitsPerTurn;
        if (type == typeof(UInt16) || type == typeof(UInt32) || type == typeof(Byte) || type == typeof(UInt64))
        {
            units = ((units % turn) + turn) % turn;
        }
        else if (type == typeof(Int16))
        {
            units = ((units + turn / 2) % turn + turn) % turn - turn / 2;
        }

        return Convert.ChangeType(units, type, CultureInfo.InvariantCulture);
    }

    public static string Format(double degrees) => degrees.ToString("0.##", CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out double degrees)
    {
        return double.TryParse(text?.Trim().TrimEnd('°'), NumberStyles.Float, CultureInfo.InvariantCulture, out degrees) && double.IsFinite(degrees);
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.DegreesText)
            .Skip(1)
            .Where(_ => !_isShowingValue)
            .Subscribe(text =>
            {
                if (!TryParse(text, out var degrees))
                {
                    return;
                }

                var units = ToUnits(degrees, Property.PropertyType);
                if (!Equals(units, CurrentValue))
                {
                    SetCurrentValue(units);
                }
            })
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        _isShowingValue = true;
        try
        {
            // Keeps what's being typed while it still means the value, "9" doesn't turn into "9.01" under the caret
            if (!TryParse(DegreesText, out var typed) || !Equals(ToUnits(typed, Property.PropertyType), CurrentValue))
            {
                DegreesText = Format(ToDegrees(CurrentValue));
            }
        }
        finally
        {
            _isShowingValue = false;
        }
    }
}
