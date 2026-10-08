using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Tests.Editor;

// What a field's rules turn down only shows their error. The sweep of every editor (2026-10-08) typed NaN and 1e39 (infinity) into a
// scenery's bounds, which stayed NaN whatever was typed after and couldn't be saved, and numbers out of a field's range and text longer
// than it takes were committed under the field's error
[Collection(ProjectCollection.Name)]
public sealed class TypedValueRulesTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public TypedValueRulesTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static T Shown<T>(DocumentViewModel document, string path) where T : DocumentNodeViewModel
    {
        var editor = Assert.IsAssignableFrom<T>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find(path)!).Construct());
        new Window { Content = new ContentControl { Content = editor }, Width = 600, Height = 200 }.Show();
        Pump();
        return editor;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void NumbersThatArentValuesArentTaken()
    {
        var scenery = _assets.AddScenery();
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        var light = ((IAsset)scenery).GetData<SceneryData>().AmbientLights[0];
        var field = Shown<TextFieldViewModel>(document, "Root.AssetData.AmbientLights[0].Intensity");

        foreach (var text in new[] { "NaN", "1e39", "-Infinity", "∞" })
        {
            field.Text = text;
            Pump();
            Assert.Equal(10, light.Intensity);
            Assert.False(field.ValidationContext.IsValid);
        }

        field.Text = "2.5";
        Pump();
        Assert.Equal(2.5f, light.Intensity);
        Assert.True(field.ValidationContext.IsValid);
    }

    // Editors only hear their node while they're shown: one shown again (a list's row scrolled back into view, a chunk's inspector switched
    // back to) showed the value it had when it was hidden. The sweep found the scenery's half size showing 100 while it was 0
    [AvaloniaFact]
    public void AnEditorShownAgainShowsTheValueItHasNow()
    {
        var scenery = _assets.AddScenery();
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        var intensity = document.PropertyGraph.Find("Root.AssetData.AmbientLights[0].Intensity")!;
        var field = Assert.IsType<TextFieldViewModel>(EditorDescRegistry.GetDesc(document, intensity).Construct());
        var shown = field.Activator.Activate();
        Assert.Equal("10", field.Text);
        shown.Dispose();

        intensity.SetValue(4.0f);
        using (field.Activator.Activate())
        {
            Assert.Equal("4", field.Text);
        }

        var halfSize = document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize")!;
        var vector = Assert.IsType<Vector3FieldViewModel>(EditorDescRegistry.GetDesc(document, halfSize).Construct());
        var vectorShown = vector.Activator.Activate();
        var componentShown = vector.Y.Activator.Activate();
        Assert.Equal("20", vector.Y.Text);
        componentShown.Dispose();
        vectorShown.Dispose();

        document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize.Y")!.SetValue(7.0f);
        using (vector.Activator.Activate())
        using (vector.Y.Activator.Activate())
        {
            Assert.Equal("7", vector.Y.Text);
        }
    }

    // The bounds' middle and half size are both made from its corners: a middle far enough out rounds the half size away, and the half
    // size's editor kept showing the old one. The history keeps the corners, undo puts them back exactly: through the middle's setter it
    // kept the half size there was, and the one rounded away stayed lost
    [AvaloniaFact]
    public void UndoPutsTheBoundsCornersBackExactly()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        var (min, max) = (data.BoundsMin, data.BoundsMax);
        var document = new DocumentViewModel(scenery);
        document.Initialize();
        var halfNode = document.PropertyGraph.Find("Root.AssetData.Bounds.HalfSize")!;
        var half = Assert.IsType<Vector3FieldViewModel>(EditorDescRegistry.GetDesc(document, halfNode).Construct());
        using var shown = half.Activator.Activate();
        using var componentShown = half.Y.Activator.Activate();
        Assert.Equal("20", half.Y.Text);

        document.PropertyGraph.Find("Root.AssetData.Bounds.Center.Y")!.SetValue(1e20f);
        Assert.Equal("0", half.Y.Text);
        document.PropertyGraph.Find("Root.AssetData.Bounds.Center.X")!.SetValue(12.345f);

        document.Undo();
        document.Undo();
        Assert.Equal((min, max), (data.BoundsMin, data.BoundsMax));
        Assert.Equal("20", half.Y.Text);

        document.Redo();
        Assert.Equal(0.0f, data.BoundsMax.Y - data.BoundsMin.Y);
    }

    [AvaloniaFact]
    public void NumbersOutOfTheFieldsRangeArentTaken()
    {
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance");
        var data = new ObjectInstanceData(instance) { RefListIndex = 3 };
        instance.SetData(data);
        var document = new DocumentViewModel(instance);
        document.Initialize();
        var field = Shown<TextFieldViewModel>(document, "Root.AssetData.RefListIndex");

        foreach (var text in new[] { "300", "-2" })
        {
            field.Text = text;
            Pump();
            Assert.Equal(3, data.RefListIndex);
            Assert.False(field.ValidationContext.IsValid);
        }

        field.Text = "255";
        Pump();
        Assert.Equal(255, data.RefListIndex);
        field.Text = "-1";
        Pump();
        Assert.Equal(-1, data.RefListIndex);
    }

    // The game keeps its names a byte per character: name fields only take plain ASCII, characters past it became '?' in the game's files
    [AvaloniaFact]
    public void NamesOnlyTakePlainAscii()
    {
        var particles = _project.Add(new Particles { Chunk = "levels/test" }, "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "SMOKE" });
        particles.SetData(data);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var systemName = Shown<TextFieldViewModel>(document, "Root.AssetData.ParticleSystems[0].Name");
        var alias = Shown<TextFieldViewModel>(document, "Root.Alias");

        foreach (var name in new[] { "Взрыв", "FEU_ÉTÉ", "€" })
        {
            systemName.Text = name;
            alias.Text = name;
            Pump();
            Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
            Assert.Equal("Particles", particles.Alias);
            Assert.False(systemName.ValidationContext.IsValid);
        }

        systemName.Text = "BOOM";
        alias.Text = "Beach particles";
        Pump();
        Assert.Equal("BOOM", data.ParticleSystems[0].Name);
        Assert.Equal("Beach particles", particles.Alias);
        Assert.NotNull(AssetRelocation.WhyNotName("Пляж"));
        Assert.Null(AssetRelocation.WhyNotName("Beach 2"));
    }

    [AvaloniaFact]
    public void TextLongerThanTheFieldTakesIsntTaken()
    {
        var particles = _project.Add(new Particles { Chunk = "levels/test" }, "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "SMOKE" });
        particles.SetData(data);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var field = Shown<TextFieldViewModel>(document, "Root.AssetData.ParticleSystems[0].Name");

        field.Text = new string('X', 17);
        Pump();
        Assert.Equal("SMOKE", data.ParticleSystems[0].Name);
        Assert.False(field.ValidationContext.IsValid);

        field.Text = new string('X', 16);
        Pump();
        Assert.Equal(new string('X', 16), data.ParticleSystems[0].Name);
    }
}
