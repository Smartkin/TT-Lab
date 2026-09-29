using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Tests.Editor;

// A particle system's blend mode and draw list are picked by what the game does with them
[Collection(ProjectCollection.Name)]
public sealed class ParticleBlendModeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void OnlyTheListsTheGameDrawsAreDrawn()
    {
        // List 0 draws the modes 0 to 3, list 1 nothing, list 2 only the Distortion mode, which always goes in it
        Assert.True(ParticleBlendModes.IsDrawn(ParticleBlendModes.Additive, 0));
        Assert.True(ParticleBlendModes.IsDrawn(ParticleBlendModes.Cutout, 0));
        Assert.False(ParticleBlendModes.IsDrawn(ParticleBlendModes.Additive, 1));
        Assert.False(ParticleBlendModes.IsDrawn(ParticleBlendModes.PageMaterial, 2));
        Assert.True(ParticleBlendModes.IsDrawn(ParticleBlendModes.Distortion, 0));
        Assert.True(ParticleBlendModes.IsDrawn(ParticleBlendModes.Distortion, 1));
        Assert.False(ParticleBlendModes.IsDrawn(5, 0));
        Assert.False(ParticleBlendModes.IsDrawn(ParticleBlendModes.Additive, 3));

        byte[] modes = [ParticleBlendModes.Additive, ParticleBlendModes.Distortion, ParticleBlendModes.Subtractive, ParticleBlendModes.Cutout, ParticleBlendModes.PageMaterial];
        Assert.Equal([ParticleBlendModes.Cutout, ParticleBlendModes.PageMaterial, ParticleBlendModes.Subtractive, ParticleBlendModes.Additive, ParticleBlendModes.Distortion],
            modes.OrderBy(ParticleBlendModes.DrawOrder));
        Assert.Equal("Not a mode", ParticleBlendModes.FindMode(5).Name);
        Assert.Equal("Never drawn", ParticleBlendModes.FindDrawList(1).Name);
    }

    [AvaloniaFact]
    public void BlendModeIsPickedByNameAndShowsWhatUndoPutsBack()
    {
        var particles = _project.Add(new Particles(), "Particles");
        var data = new ParticleData(particles);
        data.ParticleSystems.Add(new ParticleSystem { Name = "Fire" });
        particles.SetData(data);
        var document = new DocumentViewModel(particles);
        document.Initialize();
        var node = document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].BlendMode")!;
        var field = Assert.IsType<ByteChoiceFieldViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        var window = new Window { Content = new ContentControl { Content = field }, Width = 400, Height = 100 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ParticleBlendModes.Modes, field.Choices);
        Assert.Equal("Additive", field.SelectedChoice!.Name);

        field.SelectedChoice = ParticleBlendModes.FindMode(ParticleBlendModes.Subtractive);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ParticleBlendModes.Subtractive, data.ParticleSystems[0].BlendMode);

        document.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ParticleBlendModes.Additive, data.ParticleSystems[0].BlendMode);
        Assert.Equal("Additive", field.SelectedChoice!.Name);

        var list = Assert.IsType<ByteChoiceFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].DrawFlag")!).Construct());
        Assert.Equal("Drawn", list.SelectedChoice!.Name);
        window.Close();
    }
}
