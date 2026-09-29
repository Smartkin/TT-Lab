using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using Twinsanity.TwinsanityInterchange.Common;
using Color = Avalonia.Media.Color;

namespace TT_Lab.Tests.Editor;

// Particle systems' curves are 8 keys of a time in the particle's life and a value, ending at the first key at 1
[Collection(ProjectCollection.Name)]
public sealed class ParticleCurveEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static CurveKey[] Keys(params (float Time, float Value)[] keys)
    {
        var result = Enumerable.Repeat(new CurveKey(0, 0), ParticleCurveKeys.MaxKeys).ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            result[i] = new CurveKey(keys[i].Time, keys[i].Value);
        }

        return result;
    }

    private static Vector2[] Curve(params (float Time, float Value)[] keys) => Keys(keys).Select(ParticleCurveKeys.ToCurveKey).ToArray();

    private (DocumentViewModel Document, ParticleSystem System) OpenSystem()
    {
        var particles = _project.Add(new Particles(), "Particles");
        var data = new ParticleData(particles);
        var system = new ParticleSystem
        {
            Name = "Fire",
            AlphaGradient = Curve((0, 0), (0.5f, 100), (1, 0)),
            ColorGradients = Keys((0, 255), (1, 0)).Select(key => new Vector4(key.Time, key.Value0, 128, 0)).ToArray(),
        };
        data.ParticleSystems.Add(system);
        particles.SetData(data);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        return (document, system);
    }

    private static (T Editor, Window Window) Show<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsType<T>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 500, Height = 400 };
        window.Show();
        Pump();
        return (editor, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void KeysStayInOrderAndTheLastOneEndsTheCurve()
    {
        var keys = Keys((0, 0), (0.25f, 1), (0.5f, 2), (1, 3));

        Assert.Equal(0.5f, ParticleCurveKeys.Move(keys, 1, 0.9f)[1].Time);
        Assert.Equal(0.25f, ParticleCurveKeys.Move(keys, 2, 0.1f)[2].Time);
        Assert.Equal(0.0f, ParticleCurveKeys.Move(keys, 0, 0.3f)[0].Time);
        Assert.Equal(1.0f, ParticleCurveKeys.Move(keys, 3, 0.3f)[3].Time);
        // A key at 1 would end the curve early
        var last = Keys((0, 0), (0.5f, 1), (1, 2));
        Assert.True(ParticleCurveKeys.Move(last, 1, 1.0f)[1].Time < 1.0f);
        Assert.Equal(3, ParticleCurveKeys.Count(ParticleCurveKeys.Move(last, 1, 1.0f)));
    }

    [Fact]
    public void AddedKeysGoWhereTheCurveIs()
    {
        var keys = Keys((0, 0), (1, 100));

        var index = ParticleCurveKeys.Insert(keys, 0.25f, out var result);

        Assert.Equal(1, index);
        Assert.Equal(new CurveKey(0.25f, 25), result[1]);
        Assert.Equal(new CurveKey(1, 100), result[2]);
        Assert.Equal(3, ParticleCurveKeys.Count(result));
    }

    [Fact]
    public void CurvesHaveAtMostEightKeys()
    {
        var keys = Enumerable.Range(0, 8).Select(i => new CurveKey(i / 7.0f, i)).ToArray();

        Assert.False(ParticleCurveKeys.CanInsert(keys));
        Assert.Equal(-1, ParticleCurveKeys.Insert(keys, 0.5f, out _));
    }

    [Fact]
    public void RemovingKeysKeepsTheCurvesEnds()
    {
        var keys = Keys((0, 0), (0.5f, 5), (1, 9));

        Assert.False(ParticleCurveKeys.CanRemove(keys, 0));
        Assert.False(ParticleCurveKeys.CanRemove(keys, 2));
        var result = ParticleCurveKeys.Remove(keys, 1);

        Assert.Equal(2, ParticleCurveKeys.Count(result));
        Assert.Equal(new CurveKey(1, 9), result[1]);
        Assert.False(ParticleCurveKeys.CanRemove(result, 1));
    }

    [AvaloniaFact]
    public void CurvesAreEditedOnTheirGraph()
    {
        var (document, system) = OpenSystem();
        var (editor, window) = Show<ParticleCurveViewModel>(document, "Root.AssetData.ParticleSystems[0].AlphaGradient");

        Assert.Equal([new Point(0, 0), new Point(0.5, 100), new Point(1, 0)], editor.Points);
        var graph = window.GetVisualDescendants().OfType<CurveGraph>().Single();
        Assert.True(graph.Bounds.Width > 100, "The graph didn't get the inspector's width");

        editor.DragKey(1, new Point(0.25, 64));
        Assert.Equal((0.25f, 64f), (system.AlphaGradient[1].X, system.AlphaGradient[1].Y));
        Assert.True(document.IsDirty);

        editor.AddKey(new Point(0.75, 32));
        Assert.Equal(4, editor.KeyCount);
        Assert.Equal(2, editor.SelectedIndex);
        Assert.Equal((0.75f, 32f), (system.AlphaGradient[2].X, system.AlphaGradient[2].Y));

        editor.SelectedValue = "80";
        Assert.Equal(80f, system.AlphaGradient[2].Y);

        editor.RemoveKey(1);
        Assert.Equal([new Point(0, 0), new Point(0.75, 80), new Point(1, 0)], editor.Points);

        // A key put in moves the ones after it, one undo takes all of that back
        document.History.CloseStep();
        var before = editor.Points;
        editor.AddKey(new Point(0.5, 10));
        Assert.Equal(4, editor.KeyCount);
        document.Undo();
        Assert.Equal(before, editor.Points);
        document.Redo();
        Assert.Equal(4, editor.KeyCount);
        document.Undo();

        // A drag is one step however far it goes
        editor.BeginDrag();
        editor.DragKey(1, new Point(0.6, 20));
        editor.DragKey(1, new Point(0.9, 30));
        editor.EndDrag();
        document.Undo();
        Assert.Equal(before, editor.Points);
    }

    [AvaloniaFact]
    public void ColorsArePickedForTheSelectedStop()
    {
        var (document, system) = OpenSystem();
        var (editor, _) = Show<ParticleGradientViewModel>(document, "Root.AssetData.ParticleSystems[0].ColorGradients");

        Assert.Equal([Color.FromRgb(255, 128, 0), Color.FromRgb(0, 128, 0)], editor.Stops.Select(stop => stop.Color));

        editor.SelectedIndex = 1;
        editor.SelectedColor = Color.FromRgb(10, 20, 30);
        Pump();

        Assert.Equal((1f, 10f, 20f, 30f), (system.ColorGradients[1].X, system.ColorGradients[1].Y, system.ColorGradients[1].Z, system.ColorGradients[1].W));

        editor.AddStop(0.5);
        Assert.Equal(3, editor.KeyCount);
        Assert.Equal((0.5f, 132.5f, 74f, 15f), (system.ColorGradients[1].X, system.ColorGradients[1].Y, system.ColorGradients[1].Z, system.ColorGradients[1].W));
    }
}
