using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;
using Color = Avalonia.Media.Color;

namespace TT_Lab.ViewModels.Editors.Instance;

/// <summary>
/// Edits the keys of one of a particle system's curves, see <see cref="ParticleCurveKeys"/> for how the game reads them
/// </summary>
public abstract partial class ParticleKeysViewModel<T> : DocumentDataViewModel<T[]> where T : class
{
    [Reactive]
    private int _selectedIndex = -1;

    [Reactive]
    private string _selectedTime = string.Empty;

    private bool _isShowingSelection;

    protected ParticleKeysViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }

    public int KeyCount { get; private set; }

    public string Hint => "Drag the keys, double click to add one and right click to remove one. The last key ends the curve at the end of the particle's life";

    protected CurveKey[] CurrentKeys => ToKeys(CurrentValue ?? []);

    protected abstract CurveKey[] ToKeys(T[] keys);

    protected abstract T FromKey(CurveKey key);

    protected abstract void ShowKeys(IReadOnlyList<CurveKey> keys);

    protected abstract void ShowSelectedKey(CurveKey key);

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        // Keys can get edited as themselves too, in the chunk's document and in the particles' own
        foreach (var key in Property.Children)
        {
            key.Changed += Refresh;
            Disposable.Create(() => key.Changed -= Refresh).DisposeWith(disposables);
        }

        this.WhenAnyValue(x => x.SelectedIndex).Subscribe(_ => ShowSelection()).DisposeWith(disposables);
        this.WhenAnyValue(x => x.SelectedTime).Skip(1).Where(_ => !_isShowingSelection).Subscribe(text =>
        {
            if (TryParse(text, out var time) && SelectedIndex >= 0)
            {
                MoveKey(SelectedIndex, time);
            }
        }).DisposeWith(disposables);
        Refresh();
        if (SelectedIndex < 0 && KeyCount > 0)
        {
            SelectedIndex = 0;
        }
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        Refresh();
    }

    public void MoveKey(int index, float time)
    {
        Write(ParticleCurveKeys.Move(CurrentKeys, index, time));
    }

    public void SetKeyValues(int index, CurveKey values)
    {
        Write(ParticleCurveKeys.SetValues(CurrentKeys, index, values));
    }

    public void AddKey(float time, CurveKey? values = null)
    {
        var index = ParticleCurveKeys.Insert(CurrentKeys, time, out var keys);
        if (index == -1)
        {
            return;
        }

        if (values.HasValue)
        {
            keys = ParticleCurveKeys.SetValues(keys, index, values.Value);
        }

        Write(keys);
        SelectedIndex = index;
    }

    public void RemoveKey(int index)
    {
        var keys = CurrentKeys;
        if (!ParticleCurveKeys.CanRemove(keys, index))
        {
            return;
        }

        Write(ParticleCurveKeys.Remove(keys, index));
        SelectedIndex = Math.Min(index, KeyCount - 1);
    }

    // Only the keys that changed, every change updates the viewport's emitters
    protected void Write(CurveKey[] keys)
    {
        var current = CurrentKeys;
        var elements = Property.Children;
        for (var i = 0; i < keys.Length && i < elements.Count; i++)
        {
            if (keys[i] != current[i])
            {
                elements[i].SetValue(FromKey(keys[i]));
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        var keys = CurrentKeys;
        KeyCount = ParticleCurveKeys.Count(keys);
        ShowKeys(keys.Take(KeyCount).ToList());
        this.RaisePropertyChanged(nameof(KeyCount));
        if (SelectedIndex >= KeyCount)
        {
            SelectedIndex = KeyCount - 1;
        }

        ShowSelection();
    }

    private void ShowSelection()
    {
        var keys = CurrentKeys;
        if (SelectedIndex < 0 || SelectedIndex >= keys.Length)
        {
            return;
        }

        _isShowingSelection = true;
        SelectedTime = Format(keys[SelectedIndex].Time);
        ShowSelectedKey(keys[SelectedIndex]);
        _isShowingSelection = false;
    }

    protected bool IsShowingSelection => _isShowingSelection;

    protected static string Format(float value) => value.ToString("0.####", CultureInfo.CurrentCulture);

    protected static bool TryParse(string? text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && float.IsFinite(value);
    }
}

/// <summary>
/// A curve of one value over the particle's life, like its alpha, size or angle
/// </summary>
public partial class ParticleCurveViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies)
    : ParticleKeysViewModel<Vector2>(document, node, dependencies)
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<Point> _points = [];

    [Reactive]
    private string _selectedValue = string.Empty;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedValue).Skip(1).Where(_ => !IsShowingSelection).Subscribe(text =>
        {
            if (TryParse(text, out var value) && SelectedIndex >= 0)
            {
                SetKeyValues(SelectedIndex, new CurveKey(0, value));
            }
        }).DisposeWith(disposables);
    }

    public void DragKey(int index, Point to)
    {
        var keys = ParticleCurveKeys.Move(CurrentKeys, index, (float)to.X);
        Write(ParticleCurveKeys.SetValues(keys, index, new CurveKey(0, (float)to.Y)));
    }

    public void AddKey(Point at) => AddKey((float)at.X, new CurveKey(0, (float)at.Y));

    protected override CurveKey[] ToKeys(Vector2[] keys) => ParticleCurveKeys.FromCurve(keys);

    protected override Vector2 FromKey(CurveKey key) => ParticleCurveKeys.ToCurveKey(key);

    protected override void ShowKeys(IReadOnlyList<CurveKey> keys)
    {
        Points = keys.Select(key => new Point(key.Time, key.Value0)).ToList();
    }

    protected override void ShowSelectedKey(CurveKey key)
    {
        SelectedValue = Format(key.Value0);
    }
}

/// <summary>
/// The particle's color over its life, red, green and blue from 0 to 255 where 128 is the texture's own color
/// </summary>
public partial class ParticleGradientViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies)
    : ParticleKeysViewModel<Vector4>(document, node, dependencies)
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<GradientBarStop> _stops = [];

    [Reactive]
    private Color _selectedColor;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedColor).Skip(1).Where(_ => !IsShowingSelection).Subscribe(color =>
        {
            if (SelectedIndex >= 0)
            {
                SetKeyValues(SelectedIndex, new CurveKey(0, color.R, color.G, color.B));
            }
        }).DisposeWith(disposables);
    }

    public void AddStop(double time) => AddKey((float)time);

    protected override CurveKey[] ToKeys(Vector4[] keys) => ParticleCurveKeys.FromGradient(keys);

    protected override Vector4 FromKey(CurveKey key) => ParticleCurveKeys.ToGradientKey(key);

    protected override void ShowKeys(IReadOnlyList<CurveKey> keys)
    {
        Stops = keys.Select(key => new GradientBarStop(key.Time, ToColor(key))).ToList();
    }

    protected override void ShowSelectedKey(CurveKey key)
    {
        SelectedColor = ToColor(key);
    }

    private static Color ToColor(CurveKey key)
    {
        return Color.FromRgb(ToByte(key.Value0), ToByte(key.Value1), ToByte(key.Value2));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0.0f, 255.0f);
}
