using System.Text;
using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Assets;

// Create Asset makes a sound effect of the wave file picked: it plays at the pitch of the wave's rate and loops what the wave's sampler chunk
// loops, like a wave replaced in the sound's editor. New sounds kept 22050 Hz's pitch, and the PS2 played a 44100 Hz wave at half speed
[Collection(ProjectCollection.Name)]
public sealed class SoundEffectCreationTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly Package _package;

    public SoundEffectCreationTests()
    {
        _package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(_package.Name, "Sounds"));
    }

    public void Dispose() => _project.Dispose();

    private static byte[] Samples(short channels, int samples)
    {
        var pcm = new byte[samples * channels * 2];
        for (var i = 0; i < samples * channels; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), (short)(Math.Sin(i / 10.0) * 8000));
        }

        return pcm;
    }

    private string WriteWave(short channels, uint rate, int samples)
    {
        var path = Path.Combine(_project.Root, $"wave_{channels}_{rate}_{samples}.wav");
        using var writer = new BinaryWriter(File.Create(path));
        Riff.SaveRiff(writer, Samples(channels, samples), ref channels, ref rate);
        return path;
    }

    private static void Chunk(BinaryWriter writer, string id, byte[] data)
    {
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(data.Length);
        writer.Write(data);
    }

    // A mono wave with a sampler chunk's loop, its end the loop's last sample like audio tools write it
    private string WriteLoopingWave(uint rate, int samples, int loopStart, int loopLast)
    {
        var path = Path.Combine(_project.Root, "looping.wav");
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        var format = new byte[16];
        BitConverter.TryWriteBytes(format.AsSpan(0), (short)1);
        BitConverter.TryWriteBytes(format.AsSpan(2), (short)1);
        BitConverter.TryWriteBytes(format.AsSpan(4), rate);
        BitConverter.TryWriteBytes(format.AsSpan(8), rate * 2);
        BitConverter.TryWriteBytes(format.AsSpan(12), (short)2);
        BitConverter.TryWriteBytes(format.AsSpan(14), (short)16);
        Chunk(writer, "fmt ", format);
        Chunk(writer, "data", Samples(1, samples));
        var sampler = new byte[60];
        BitConverter.TryWriteBytes(sampler.AsSpan(28), 1);
        BitConverter.TryWriteBytes(sampler.AsSpan(44), loopStart);
        BitConverter.TryWriteBytes(sampler.AsSpan(48), loopLast);
        Chunk(writer, "smpl", sampler);
        return path;
    }

    private SoundEffect? Create(string name, string wavePath)
    {
        return (SoundEffect?)AssetFactory.CreateAsset(typeof(SoundEffect), _project.GetFolder(_package, "Sounds"), name, string.Empty,
            TwinIdGeneratorServiceProvider.GetGenerator<SoundEffect>(), asset => AssetDataFactory.CreateSoundEffectData(asset, wavePath));
    }

    [AvaloniaFact]
    public void ASoundIsMadeOfTheWaveFileItIsGiven()
    {
        var sound = Create("Waves", WriteWave(1, 22050, 2900));

        Assert.NotNull(sound);
        Assert.True(File.Exists(sound.FullDataPath));
        var data = ((IAsset)sound).GetData<SoundEffectData>();
        Assert.Equal(22050u, data.GetFrequency());
        Assert.False(data.IsStereo());
        Assert.Equal(2900 * 2, data.GetPcm().Length);
        Assert.Equal((-1, -1), (sound.LoopStart, sound.LoopEnd));
    }

    [AvaloniaFact]
    public void AStereoWaveOverHalfTheRateMakesNoSound()
    {
        Assert.Null(Create("Loud", WriteWave(2, 44100, 100)));
    }

    [AvaloniaFact]
    public void ASoundPlaysAtItsWavesRate()
    {
        var sound = Create("Seagulls", WriteWave(1, 44100, 2900))!;

        Assert.Equal(ITwinSound.PitchOf(44100), sound.Pitch);
        Assert.Equal(3763, sound.Pitch);
        // Kept with the asset
        Assert.Contains("\"Pitch\": 3763", File.ReadAllText(Path.Combine(sound.FullPath, $"{sound.Name}.json")));
        Assert.Equal(ITwinSound.PitchOf(11025), Create("Drums", WriteWave(1, 11025, 2900))!.Pitch);
    }

    // In whole blocks of 28 samples, the PS2 keeps loops in its ADPCM blocks' flags
    [AvaloniaFact]
    public void ASoundLoopsWhatItsWaveLoops()
    {
        var sound = Create("Engine", WriteLoopingWave(32000, 1000, 30, 299))!;

        Assert.Equal((28, 308), (sound.LoopStart, sound.LoopEnd));
        Assert.Equal(ITwinSound.PitchOf(32000), sound.Pitch);
    }

    private static void Build(IAsset asset, ITwinItemFactory build, string chunk)
    {
        var factory = build.ForChunk();
        factory.ChunkPath = chunk;
        using var scope = new AssetDataScope();
        asset.ResolveChunkResources(factory, factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_SOUND_EFFECTS_SECTION));
    }

    // A sound made with Create Asset before kept 22050 Hz's pitch whatever its wave, which a user can also have picked: the build tells it,
    // once for every chunk having the sound
    [AvaloniaFact]
    public void ASoundAt22050HzOfAWaveOfAnotherRateIsWarnedAbout()
    {
        var old = Create("Seagulls", WriteWave(1, 44100, 2900))!;
        old.Pitch = ITwinSound.PitchOf(22050);
        var fitting = Create("Waves", WriteWave(1, 22050, 2900))!;
        var picked = Create("Drums", WriteWave(1, 11025, 2900))!;
        picked.Pitch = ITwinSound.PitchOf(32000);
        var build = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2 };

        foreach (var sound in new[] { old, fitting, picked })
        {
            Build(sound, build, "levels/test/first");
            Build(sound, build, "levels/test/second");
        }

        Assert.StartsWith("Seagulls plays at 22050 Hz but its wave is 44100 Hz", Assert.Single(build.Warned.Keys));
    }
}
