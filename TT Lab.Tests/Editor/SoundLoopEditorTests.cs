using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Controls;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets.Code;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Code;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;

namespace TT_Lab.Tests.Editor;

// A sound's loop is kept in its asset's metadata, read off the ADPCM blocks when the disc is imported and edited on the waveform of its
// editor in whole blocks of 28 samples
[Collection(ProjectCollection.Name)]
public sealed class SoundLoopEditorTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private SoundEffect AddSound(int samples)
    {
        var sound = _project.Add(new SoundEffect(), "Waves");
        var path = Path.Combine(_project.Root, "waves.wav");
        var pcm = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), (short)(Math.Sin(i / 10.0) * 8000));
        }

        using (var writer = new BinaryWriter(File.Create(path)))
        {
            short channels = 1;
            uint rate = 22050;
            Riff.SaveRiff(writer, pcm, ref channels, ref rate);
        }

        sound.SetData(new SoundEffectData(sound, path));
        return sound;
    }

    private static SoundEffectViewModel Editor(DocumentViewModel document) =>
        Assert.IsType<SoundEffectViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData[custom_editor]")!).Construct());

    [AvaloniaFact]
    public void TheLoopIsSetInBlocksAndEachChangeIsOneStep()
    {
        var sound = AddSound(2900);
        var document = new DocumentViewModel(sound);
        document.Initialize();
        var editor = Editor(document);
        Assert.Equal("Plays once", editor.LoopText);
        Assert.Equal(2900, editor.DisplaySamples.Length);

        // The whole sound, but for the last block that isn't full
        editor.LoopWholeSound();
        Assert.Equal((0, 2884), (sound.LoopStart, sound.LoopEnd));
        Assert.StartsWith("Loops from 0.000 s to 0.131 s (samples 0 to 2884), the 0.001 s after", editor.LoopText);
        document.Undo();
        Assert.Equal((-1, -1), (sound.LoopStart, sound.LoopEnd));
        document.Redo();

        // A drag is one step, its ends snap to blocks and stay a block apart
        editor.BeginLoopDrag();
        editor.DragLoopStart(100);
        editor.DragLoopStart(300);
        editor.EndLoopDrag();
        Assert.Equal(308, sound.LoopStart);
        editor.DragLoopEnd(310);
        Assert.Equal(336, sound.LoopEnd);
        document.Undo();
        Assert.Equal((308, 2884), (sound.LoopStart, sound.LoopEnd));
        document.Undo();
        Assert.Equal((0, 2884), (sound.LoopStart, sound.LoopEnd));

        editor.RemoveLoop();
        Assert.Equal((-1, -1), (sound.LoopStart, sound.LoopEnd));
        Assert.False(editor.HasLoop);
        // Values typed in the inspector show on the waveform
        document.PropertyGraph.Find("Root.LoopEnd")!.SetValue(560);
        document.PropertyGraph.Find("Root.LoopStart")!.SetValue(28);
        Assert.True(editor.HasLoop);
        Assert.Equal((28, 560), (editor.LoopStart, editor.LoopEnd));
    }

    [AvaloniaFact]
    public void TheLoopsHandlesDragOnTheWaveform()
    {
        var sound = AddSound(2800);
        sound.LoopStart = 0;
        sound.LoopEnd = 2800;
        var document = new DocumentViewModel(sound);
        document.Initialize();
        var window = new Window { Content = new ContentControl { Content = Editor(document) }, Width = 800, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var waveform = window.GetVisualDescendants().OfType<SoundWaveform>().Single();
        Assert.Equal(2800, waveform.Samples!.Length);
        Point At(int sample) => waveform.TranslatePoint(new Point(sample / 2800.0 * waveform.Bounds.Width, waveform.Bounds.Height / 2), window)!.Value;

        window.MouseDown(At(0), MouseButton.Left);
        window.MouseMove(At(500));
        window.MouseMove(At(1000));
        window.MouseUp(At(1000), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, sound.LoopStart % 28);
        Assert.InRange(sound.LoopStart, 980, 1036);
        Assert.Equal(sound.LoopStart, waveform.LoopStart);
        document.Undo();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, sound.LoopStart);
        Assert.Equal(0, waveform.LoopStart);

        // Clicking away from the handles moves playback, not the loop
        window.MouseDown(At(2000), MouseButton.Left);
        window.MouseUp(At(2000), MouseButton.Left);
        Assert.Equal((0, 2800), (sound.LoopStart, sound.LoopEnd));
        window.Close();
    }

    [AvaloniaFact]
    public void ImportedSoundsKeepTheirLoop()
    {
        var item = new PS2AnySound { Header = 3, Pitch = 1881, Param1 = 32, Param2 = 16, Param3 = 8192, Param4 = 8192 };
        item.SetDataFromPCM(new byte[280 * 2], 28, 280);

        var sound = new SoundEffect(_project.Project.GlobalPackagePS2.URI, false, "", 0x10, "Ambience", item);

        Assert.Equal((28, 280), (sound.LoopStart, sound.LoopEnd));
        // Sounds played once and sounds made in TT Lab have none
        item.SetDataFromPCM(new byte[280 * 2]);
        Assert.Equal((-1, -1), (new SoundEffect(_project.Project.GlobalPackagePS2.URI, false, "", 0x11, "Boing", item).LoopStart, item.LoopEnd));
        Assert.Equal(-1, new SoundEffect().LoopStart);
    }
}
