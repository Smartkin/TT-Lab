using TT_Lab.Libraries;
using State = TT_Lab.Libraries.Ps2ImageMaker.ProgressState;

namespace TT_Lab.Tests.Build;

// The native packer's packings the way TT Lab waits for them, with polls made up here (the tests' output has no native libraries).
// PS2ImageMaker before its fix wrote zero sectors past the image's end until the memory ran out (a count of them left unset): an image
// growing past what its folder's files make stops Build ISO with an error
public sealed class Ps2ImagePackerTests : IDisposable
{
    private const long SectorSize = 2048;
    private readonly string _root = Directory.CreateTempSubdirectory("TTLabPs2Packer_").FullName;
    private int _started;

    public Ps2ImagePackerTests()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(Path.Combine(Folder, "DATA.BIN"), new byte[8000]);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private string Folder => Path.Combine(_root, "disc");

    private string Image => Path.Combine(_root, "image.iso.part");

    private static Ps2ImageMaker.Progress Progress(State state, float done, string file = "") => new()
    {
        ProgressS = state, ProgressPercentage = done, Finished = state is State.Finished or State.Failed, File = file
    };

    // A packer whose packing's thread is the polls: what each poll gives, having written what it writes
    private Ps2ImagePacker Packer(Func<Ps2ImageMaker.Progress> poll) => new((_, _) =>
    {
        _started++;
        return Progress(State.EnumFiles, 0);
    }, poll) { PollInterval = TimeSpan.Zero };

    // The image as long as the packing's thread has written it
    private void Write(long bytes)
    {
        using var image = new FileStream(Image, FileMode.OpenOrCreate, FileAccess.Write);
        image.SetLength(bytes);
    }

    [Fact]
    public void APackingThatFinishesReportsItsPolls()
    {
        var polls = new Queue<(long Bytes, Ps2ImageMaker.Progress Progress)>();
        polls.Enqueue((100_000, Progress(State.WriteFiles, 0.5f)));
        polls.Enqueue((272 * SectorSize, Progress(State.Finished, 1)));
        var reported = new List<float>();
        Packer(() =>
        {
            var (bytes, progress) = polls.Dequeue();
            Write(bytes);
            return progress;
        }).Pack(Folder, Image, reported.Add);

        Assert.Equal(new[] { 0.5f, 1f }, reported);
        Assert.Equal(1, _started);
    }

    [Fact]
    public void AnImageGrowingPastWhatItsFilesMakeStopsWithAnErrorAndNoOtherPackingStarts()
    {
        var most = Ps2ImagePacker.MostImageBytes(Folder);
        var written = 272 * SectorSize;
        // The runaway: zero sectors past the image's end for good
        var packer = Packer(() =>
        {
            if (written > 64 * most)
            {
                throw new InvalidOperationException($"Still waiting for an image of {written} bytes");
            }

            written += 256 << 10;
            Write(written);
            return Progress(State.WriteEnd, 0.9f);
        });

        var ranAway = Assert.Throws<Ps2ImageRunawayException>(() => packer.Pack(Folder, Image));
        Assert.Contains("ran away", ranAway.Message);
        // Its thread can't be stopped: what to do about it, now, and what's left of the image
        Assert.Contains("Close TT Lab now", ranAway.Message);
        Assert.Contains("the image wasn't made, the last one is left as it was", ranAway.Message);
        // Stopped at the first poll past it
        Assert.InRange(new FileInfo(Image).Length, most + 1, most + (256 << 10));
        // Its thread goes on writing with the library's progress: no other packing starts
        var refused = Assert.Throws<Ps2ImageRunawayException>(() => packer.Pack(Folder, Path.Combine(_root, "next.iso.part")));
        Assert.Contains("still writing", refused.Message);
        Assert.Contains("close TT Lab now", refused.Message);
        Assert.Equal(1, _started);
    }

    [Fact]
    public void AFinishedImageBiggerThanItsFilesMakeIsAnError()
    {
        var packer = Packer(() =>
        {
            Write(Ps2ImagePacker.MostImageBytes(Folder) + SectorSize);
            return Progress(State.Finished, 1);
        });

        Assert.Contains("past the image's end", Assert.Throws<IOException>(() => packer.Pack(Folder, Image)).Message);
        // It stopped writing: the next packing starts
        Assert.Throws<IOException>(() => packer.Pack(Folder, Image));
        Assert.Equal(2, _started);
    }

    [Fact]
    public void AFailedPackingNamesTheFileItStoppedAt()
    {
        var packer = Packer(() => Progress(State.Failed, 0.4f, "Crash.BD"));

        Assert.Contains("it stopped at Crash.BD", Assert.Throws<IOException>(() => packer.Pack(Folder, Image)).Message);
        Assert.Throws<IOException>(() => packer.Pack(Folder, Image));
        Assert.Equal(2, _started);
    }

    [Fact]
    public void TheMostAnImageTakesHoldsWhatThePackerMakes()
    {
        // PS2ImageMaker's image of a folder of one file of 8000 bytes (its regression test's): 267 sectors before the file, its 4 and the end of
        // session descriptor
        var oneFile = Ps2ImagePacker.MostImageBytes(Folder);
        Assert.True(oneFile >= 272 * SectorSize);
        // A file's every sector, in a folder of its own
        Directory.CreateDirectory(Path.Combine(Folder, "FMV"));
        File.WriteAllBytes(Path.Combine(Folder, "FMV", "ATTRACT.PSS"), new byte[5 * SectorSize + 1]);
        Assert.True(Ps2ImagePacker.MostImageBytes(Folder) >= oneFile + 6 * SectorSize);
    }
}
