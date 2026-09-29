using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;

namespace TT_Lab.Tests.Editor;

// Angles the game keeps in 65536ths of a turn are edited in degrees, a sound's pitch as the sample rate it stands for
[Collection(ProjectCollection.Name)]
public sealed class AngleAndSampleRateEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void DegreesConvertToTheGamesUnitsAndBack()
    {
        Assert.Equal(20.0, AngleFieldViewModel.ToDegrees((UInt32)3641), 1);
        Assert.Equal(180.0, AngleFieldViewModel.ToDegrees((UInt32)32768));
        Assert.Equal(-90.0, AngleFieldViewModel.ToDegrees((Int16)(-16384)));
        Assert.Equal((UInt32)16384, AngleFieldViewModel.ToUnits(90.0, typeof(UInt32)));
        // Unsigned values wrap around the turn, signed ones go the shorter way
        Assert.Equal((UInt32)49152, AngleFieldViewModel.ToUnits(-90.0, typeof(UInt32)));
        Assert.Equal((UInt32)0, AngleFieldViewModel.ToUnits(360.0, typeof(UInt32)));
        Assert.Equal((Int16)(-16384), AngleFieldViewModel.ToUnits(270.0, typeof(Int16)));
        Assert.Equal((Int16)(-32768), AngleFieldViewModel.ToUnits(180.0, typeof(Int16)));
        Assert.Equal("20", AngleFieldViewModel.Format(AngleFieldViewModel.ToDegrees((UInt32)3640)));
        Assert.True(AngleFieldViewModel.TryParse(" 45.5° ", out var parsed));
        Assert.Equal(45.5, parsed);
        Assert.False(AngleFieldViewModel.TryParse("lots", out _));
    }

    [AvaloniaFact]
    public void CamerasAndEmittersGetTheDegreeEditor()
    {
        var camera = _project.Add(new Camera { Chunk = "default" }, "Cam");
        camera.SetData(new CameraData(camera) { PitchStart = 16384, YawEnd = 32768 });
        var document = new DocumentViewModel(camera);
        document.Initialize();

        var pitch = Assert.IsType<AngleFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.PitchStart")!).Construct());
        pitch.Activator.Activate();
        Assert.Equal("90", pitch.DegreesText);
        // The value changing from elsewhere, like undo, shows up in degrees
        document.PropertyGraph.Find("Root.AssetData.PitchStart")!.SetValue((UInt32)8192);
        Assert.Equal("45", pitch.DegreesText);
        // Typing degrees sets the game's units
        pitch.DegreesText = "-90";
        Assert.Equal((UInt32)49152, ((CameraData)camera.GetData()).PitchStart);
        Assert.Equal("180", Assert.IsType<AngleFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.YawEnd")!).Construct()).DegreesText);
    }

    [AvaloniaFact]
    public void SoundsPickTheirRateFromTheKnownOnes()
    {
        var sound = _project.Add(new SoundEffect(), "Boing");
        sound.Pitch = 1881;
        var document = new DocumentViewModel(sound);
        document.Initialize();

        var rate = Assert.IsType<SampleRateFieldViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.Pitch")!).Construct());
        rate.Activator.Activate();
        Assert.Equal(22050, rate.SelectedRate!.Hertz);
        Assert.Equal([8000, 11025, 12000, 16000, 22050, 24000, 32000, 44100, 48000], rate.Rates.Select(choice => choice.Hertz));
        Assert.Equal(2730, rate.Rates.Single(choice => choice.Hertz == 32000).Pitch);

        // A pitch that's no known rate is its own choice, and goes once a known one is picked
        document.PropertyGraph.Find("Root.Pitch")!.SetValue((UInt16)1000);
        Assert.Equal("Pitch 1000 (about 11719 Hz)", rate.SelectedRate!.Label);
        Assert.Equal(10, rate.Rates.Count);
        document.PropertyGraph.Find("Root.Pitch")!.SetValue((UInt16)4096);
        Assert.Equal(48000, rate.SelectedRate!.Hertz);
        Assert.Equal(9, rate.Rates.Count);
        // Picking a rate sets the pitch
        rate.SelectedRate = rate.Rates.Single(choice => choice.Hertz == 8000);
        Assert.Equal((UInt16)682, sound.Pitch);

        // Undo brings a pitch of its own back into the combo box's list and takes it out again. The list got changed in place, the combo
        // box then picked items it didn't have anymore
        var window = new Avalonia.Controls.Window { Content = new Avalonia.Controls.ContentControl { Content = rate }, Width = 400, Height = 200 };
        window.Show();
        document.PropertyGraph.Find("Root.Pitch")!.SetValue((UInt16)1000);
        document.History.CloseStep();
        rate.SelectedRate = rate.Rates.Single(choice => choice.Hertz == 8000);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(9, rate.Rates.Count);
        document.Undo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal((UInt16)1000, sound.Pitch);
        Assert.Equal("Pitch 1000 (about 11719 Hz)", rate.SelectedRate!.Label);
        Assert.Equal(10, rate.Rates.Count);
        document.Redo();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal((UInt16)682, sound.Pitch);
        Assert.Equal(8000, rate.SelectedRate!.Hertz);
        Assert.False(document.CanRedo);
        window.Close();
    }

    [Fact]
    public void TheStrongestThreeDirectionalLightsFeedTheEnvironmentMap()
    {
        var weak = new DirectionalLight { Intensity = 0.2f, Direction = new Vector4(0, 0, 2, 0) };
        var strong = new DirectionalLight { Intensity = 1.0f, Direction = new Vector4(3, 0, 0, 0) };
        var middle = new DirectionalLight { Intensity = 0.5f, Direction = new Vector4(0, 4, 0, 0) };
        var lights = EnvLights.Of([weak, strong, middle, new DirectionalLight { Intensity = 0.1f, Direction = new Vector4(1, 1, 1, 0) }]);

        Assert.Equal(new GlmSharp.vec3(1, 0, 0), lights[0]);
        Assert.Equal(new GlmSharp.vec3(0, 1, 0), lights[1]);
        Assert.Equal(new GlmSharp.vec3(0, 0, 1), lights[2]);
        // Missing lights are the defaults
        Assert.Equal(EnvLights.Defaults[1], EnvLights.Of([strong])[1]);
    }
}
