using TT_Lab.Rendering.Buffers;

namespace TT_Lab.Tests.Rendering;

// The vertex pool's models and the batches' kept instances take ranges of one growing buffer each
public class RangeAllocatorTests
{
    [Fact]
    public void RangesComeOffTheEnd()
    {
        var ranges = new RangeAllocator();

        Assert.Equal(0, ranges.Take(10));
        Assert.Equal(10, ranges.Take(5));
        Assert.Equal(15, ranges.End);
        // Nothing to take is where the next would start, without taking anything
        Assert.Equal(15, ranges.Take(0));
        Assert.Equal(15, ranges.End);
    }

    [Fact]
    public void AFreedRangeIsTakenAgainByTheSmallestThatFits()
    {
        var ranges = new RangeAllocator();
        var big = ranges.Take(10);
        ranges.Take(1);
        var small = ranges.Take(4);
        ranges.Take(1);
        ranges.Give(big, 10);
        ranges.Give(small, 4);

        Assert.Equal(small, ranges.Take(3));
        // The rest of the small one, then the big one
        Assert.Equal(small + 3, ranges.Take(1));
        Assert.Equal(big, ranges.Take(6));
        Assert.Equal(big + 6, ranges.Take(4));
        Assert.Equal(16, ranges.Take(2));
    }

    [Fact]
    public void FreedRangesJoinTheOnesNextToThem()
    {
        var ranges = new RangeAllocator();
        var first = ranges.Take(4);
        var second = ranges.Take(4);
        var third = ranges.Take(4);
        ranges.Take(1);
        ranges.Give(first, 4);
        ranges.Give(third, 4);
        ranges.Give(second, 4);

        Assert.Equal(first, ranges.Take(12));
    }

    [Fact]
    public void FreeingTheLastRangeGivesTheEndBack()
    {
        var ranges = new RangeAllocator();
        ranges.Take(4);
        var middle = ranges.Take(4);
        var last = ranges.Take(4);
        ranges.Give(middle, 4);
        ranges.Give(last, 4);

        Assert.Equal(4, ranges.End);
        Assert.Equal(4, ranges.Take(6));
    }
}
