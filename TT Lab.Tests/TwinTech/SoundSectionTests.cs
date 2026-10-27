using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.TwinTech;

// A chunk's sound section has every sound's header, with the offset of its samples, then the samples: the game reads each sound's where its
// header says (the decomp's SoundTableQueueSamples), and so does TwinTech
public class SoundSectionTests
{
    private static readonly (UInt32 Id, Boolean Stereo, Int32 Blocks)[] Sounds = [(0x30, false, 5), (0x10, true, 2), (0x20, false, 4), (0x05, true, 3), (0x15, false, 2)];

    private static ITwinSection SoundsOf(ITwinSection chunk) =>
        chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_SOUND_EFFECTS_SECTION);

    // A build puts the sounds in in the order its objects use them, and the code section sorts the records by their IDs once the samples are
    // laid out: their samples aren't in the records' order (they are in the game's files). Read back, every sound has its own samples, a
    // stereo sound both halves. Adding the lengths up in the records' order took other sounds' bytes, and creating a project from a game
    // TT Lab built got most of its sounds garbled and cut short at an end block of another sound
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EverySoundIsReadWithItsOwnSamples(Boolean inIdOrder)
    {
        var chunk = new PS2ItemFactory().GenerateRM();
        var random = new Random(5);
        var written = new Dictionary<UInt32, Byte[]>();
        foreach (var (id, stereo, blocks) in inIdOrder ? Sounds.OrderBy(sound => sound.Id) : Sounds.AsEnumerable())
        {
            var samples = new Byte[blocks * 16 * (stereo ? 2 : 1)];
            random.NextBytes(samples);
            var sound = new PS2AnySound { Header = stereo ? 2u : 3u, Sound = samples };
            sound.SetID(id);
            SoundsOf(chunk).AddItem(sound);
            written[id] = samples;
        }

        var read = SoundsOf(Deserialize(new PS2AnyTwinsanityRM2(), Serialize(chunk)));

        Assert.Equal([0x05u, 0x10u, 0x15u, 0x20u, 0x30u], Enumerable.Range(0, read.GetItemsAmount()).Select(i => read.GetItem(i).GetID()));
        Assert.All(written, pair => Assert.Equal(pair.Value, read.GetItem<PS2AnySound>(pair.Key).Sound));
    }

    // The front end's sounds are a section of their own, written in the order they're in
    [Fact]
    public void TheFrontEndsSoundsAreReadWithTheirOwnSamples()
    {
        var frontend = new PS2Frontend();
        var random = new Random(9);
        var written = new List<Byte[]>();
        foreach (var (id, stereo, blocks) in Sounds)
        {
            var samples = new Byte[blocks * 16 * (stereo ? 2 : 1)];
            random.NextBytes(samples);
            var sound = new PS2AnySound { Header = stereo ? 2u : 3u, Sound = samples };
            sound.SetID(id);
            frontend.AddItem(sound);
            written.Add(samples);
        }

        var read = Deserialize(new PS2Frontend(), Serialize(frontend));

        Assert.Equal(written, Enumerable.Range(0, read.GetItemsAmount()).Select(i => ((PS2AnySound)read.GetItem(i)).Sound));
    }
}
