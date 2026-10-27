using TT_Lab.Project;

namespace TT_Lab.Tests.Projects;

// The gate keeps builds and creation within the memory budget without stopping them: work goes on one at a time when there's no room, the
// work running grows by one at a time while there is, collections run one at a time and are put off while they free little. A creation
// stalled for good with hundreds of collections running at once, builds crawled under a collection every second while TT Lab held more than
// the budget's share, and every chunk waiting started at once when a collection made room
public sealed class MemoryGateTests
{
    // Start share 0.6, collect share 0.8
    private const long Budget = 1000;

    private sealed class FakeProbe : MemoryGate.IProbe
    {
        private long _workingSet;
        private long _milliseconds;
        private int _collected;

        public long WorkingSet
        {
            get => Interlocked.Read(ref _workingSet);
            set => Interlocked.Exchange(ref _workingSet, value);
        }

        public long Milliseconds
        {
            get => Interlocked.Read(ref _milliseconds);
            set => Interlocked.Exchange(ref _milliseconds, value);
        }

        public int Collected => Volatile.Read(ref _collected);

        // What a collection does to the working set and the clock
        public Action? Collecting { get; set; }

        public void Collect()
        {
            Interlocked.Increment(ref _collected);
            Collecting?.Invoke();
        }
    }

    private static MemoryGate Gate(FakeProbe probe) => new(Budget, 0.6, probe, false);

    private static async Task<bool> EntersWithin(Task entering, int milliseconds) => await Task.WhenAny(entering, Task.Delay(milliseconds)) == entering;

    // Work that blocks (waiting at the gate, a collection held open) on a thread of its own: under the full suite the pool had no thread to
    // spare for 5 s and the work the test waited for hadn't started
    private static Task Blocking(Action work) => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static Task<T> Blocking<T>(Func<T> work) => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static async Task<bool> Eventually(Func<bool> condition)
    {
        for (var tries = 0; tries < 500 && !condition(); tries++)
        {
            await Task.Delay(10);
        }

        return condition();
    }

    // Pieces of work that wait at the gate and count themselves in
    private sealed class Waiters
    {
        private int _entered;

        public Waiters(MemoryGate gate, int count)
        {
            for (var i = 0; i < count; i++)
            {
                _ = Blocking(() =>
                {
                    gate.Enter();
                    Interlocked.Increment(ref _entered);
                });
            }
        }

        public int Entered => Volatile.Read(ref _entered);

        // What enters does right away, the gate wakes the work waiting when it's told to
        public async Task<bool> StayAt(int entered)
        {
            await Task.Delay(200);
            return Entered == entered;
        }

        // The rest let in with room, a second at a time, so no task stays waiting after the test
        public async Task LetIn(MemoryGate gate, FakeProbe probe, int count)
        {
            probe.WorkingSet = 0;
            for (var tries = 0; tries < 500 && Entered < count; tries++)
            {
                probe.Milliseconds += 1000;
                gate.Check();
                await Task.Delay(10);
            }

            Assert.Equal(count, Entered);
        }
    }

    // Every chunk waiting started on the one reading of the working set a collection made room in, before any of them had allocated: a build
    // at 1500 MB went to 2.2 to 3.3 GB again and again. More work starts one piece at a time, a second after the last while there's room
    [Fact]
    public async Task ACollectionMakingRoomLetsOnePieceIn()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        gate.Enter();
        var waiters = new Waiters(gate, 4);
        Assert.True(await Eventually(() => gate.MostWaiting == 4));

        // The piece running has run a while
        probe.Milliseconds = 5000;
        probe.Collecting = () => probe.WorkingSet = 100;
        Assert.True(gate.Check());
        Assert.True(await Eventually(() => waiters.Entered == 1));
        Assert.True(await waiters.StayAt(1));

        probe.Milliseconds = 5999;
        Assert.False(gate.Check());
        Assert.True(await waiters.StayAt(1));
        probe.Milliseconds = 6000;
        Assert.False(gate.Check());
        Assert.True(await Eventually(() => waiters.Entered == 2));
        Assert.True(await waiters.StayAt(2));

        // None without room, however long the work runs
        probe.WorkingSet = 700;
        probe.Milliseconds = 60_000;
        Assert.False(gate.Check());
        Assert.True(await waiters.StayAt(2));

        await waiters.LetIn(gate, probe, 4);
    }

    // At a build's start every worker started a chunk at once
    [Fact]
    public async Task WorkStartsOnePieceAtATimeWithRoom()
    {
        var probe = new FakeProbe { WorkingSet = 100 };
        using var gate = Gate(probe);
        gate.Enter();
        var waiters = new Waiters(gate, 3);
        Assert.True(await Eventually(() => gate.MostWaiting == 3));
        Assert.Equal(0, waiters.Entered);

        probe.Milliseconds = 1000;
        Assert.False(gate.Check());
        Assert.True(await Eventually(() => waiters.Entered == 1));
        Assert.True(await waiters.StayAt(1));

        await waiters.LetIn(gate, probe, 3);
    }

    // Small work (an asset written) finishes thousands of times a second: each piece finishing lets the next one start and one more beside
    // it, a second apart they would be one or two at a time
    [Fact]
    public async Task APieceFinishingLetsOneMoreStartBesideTheOthers()
    {
        var probe = new FakeProbe { WorkingSet = 100 };
        using var gate = Gate(probe);
        gate.Enter();
        var waiters = new Waiters(gate, 4);
        Assert.True(await Eventually(() => gate.MostWaiting == 4));

        gate.Exit();
        Assert.True(await Eventually(() => waiters.Entered == 2));
        Assert.True(await waiters.StayAt(2));

        // One of the two finishes: its place and one more
        gate.Exit();
        Assert.True(await Eventually(() => waiters.Entered == 4));
    }

    // Pieces that finished without room give their places to nobody: once there's room again the work running grows from what runs
    [Fact]
    public async Task WithoutRoomTheWorkRunningComesDownToWhatRuns()
    {
        var probe = new FakeProbe { WorkingSet = 100 };
        using var gate = Gate(probe);
        gate.Enter();
        probe.Milliseconds = 1000;
        gate.Enter();
        probe.Milliseconds = 2000;
        gate.Enter();
        Assert.Equal(0, gate.MostWaiting);

        probe.WorkingSet = 700;
        gate.Exit();
        gate.Exit();
        var waiters = new Waiters(gate, 3);
        Assert.True(await Eventually(() => gate.MostWaiting == 3));

        probe.WorkingSet = 100;
        Assert.False(gate.Check());
        Assert.True(await Eventually(() => waiters.Entered == 1));
        Assert.True(await waiters.StayAt(1));

        await waiters.LetIn(gate, probe, 3);
    }

    // Work starting again once every piece finished (a creation's next phase) grows from one again, not straight back to what ran before
    [Fact]
    public async Task WorkStartingOverAfterAllFinishedGrowsAgain()
    {
        var probe = new FakeProbe { WorkingSet = 100 };
        using var gate = Gate(probe);
        gate.Enter();
        probe.Milliseconds = 1000;
        gate.Enter();
        probe.Milliseconds = 2000;
        gate.Enter();
        gate.Exit();
        gate.Exit();
        gate.Exit();

        gate.Enter();
        var waiters = new Waiters(gate, 3);
        Assert.True(await Eventually(() => gate.MostWaiting == 2));
        Assert.True(await Eventually(() => waiters.Entered == 1));
        Assert.True(await waiters.StayAt(1));

        await waiters.LetIn(gate, probe, 3);
    }

    // What TT Lab holds can be more than the budget, which no collection brings down
    [Fact]
    public void OnePieceOfWorkAlwaysGoesOn()
    {
        var probe = new FakeProbe { WorkingSet = 5 * Budget };
        using var gate = Gate(probe);

        gate.Enter();
        gate.Exit();
        gate.Enter();
        gate.Exit();

        Assert.Equal(0, gate.MostWaiting);
        Assert.Equal(0, probe.Collected);
    }

    [Fact]
    public async Task WithoutRoomWorkWaitsForTheWorkBeforeIt()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        gate.Enter();

        var second = Blocking(gate.Enter);
        Assert.True(await Eventually(() => gate.MostWaiting == 1));
        Assert.False(await EntersWithin(second, 200));

        gate.Exit();
        Assert.True(await EntersWithin(second, 5000));
    }

    // The work exiting collects for the work waiting at the start share, below the watchdog's
    [Fact]
    public async Task TheWorkExitingCollectsForTheWorkWaiting()
    {
        var probe = new FakeProbe { WorkingSet = 700 };
        using var gate = Gate(probe);
        gate.Enter();
        gate.Exit();
        // Nobody waited
        Assert.Equal(0, probe.Collected);
        // Under the watchdog's share
        Assert.False(gate.Check());

        gate.Enter();
        var waiting = Blocking(gate.Enter);
        Assert.True(await Eventually(() => gate.MostWaiting == 1));
        Assert.False(await EntersWithin(waiting, 200));
        probe.Collecting = () => probe.WorkingSet = 100;
        gate.Exit();

        Assert.True(await EntersWithin(waiting, 5000));
        Assert.Equal(1, probe.Collected);
    }

    [Fact]
    public async Task TheWatchdogsCollectionLetsTheWorkWaitingIn()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        gate.Enter();
        var waiting = Blocking(gate.Enter);
        Assert.True(await Eventually(() => gate.MostWaiting == 1));
        Assert.False(await EntersWithin(waiting, 200));

        probe.Milliseconds = 1000;
        probe.Collecting = () => probe.WorkingSet = 100;
        Assert.True(gate.Check());

        Assert.True(await EntersWithin(waiting, 5000));
    }

    // The pool's timers started a collection every second while the one before still ran: 258 of them waited in collections at once
    [Fact]
    public async Task OneCollectionRunsAtATime()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        using var inside = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        probe.Collecting = () =>
        {
            inside.Set();
            release.Wait();
        };

        var first = Blocking(gate.Check);
        Assert.True(inside.Wait(5000));
        probe.Milliseconds = 60_000;
        Assert.False(gate.Check());
        // Work exiting for work waiting goes on without a collection of its own
        gate.Enter();
        var waiting = Blocking(gate.Enter);
        Assert.True(await Eventually(() => gate.MostWaiting == 1));
        Assert.False(await EntersWithin(waiting, 200));
        gate.Exit();
        Assert.True(await EntersWithin(waiting, 5000));

        release.Set();
        Assert.True(await first);
        Assert.Equal(1, probe.Collected);
    }

    // A collection stops every thread: what comes after it is nine times as long, from its end
    [Fact]
    public void TheNextCollectionWaitsFromTheEndOfTheLastNineTimesItsLength()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        probe.Collecting = () =>
        {
            probe.Milliseconds += 3000;
            probe.WorkingSet = 100;
        };

        Assert.True(gate.Check());
        probe.WorkingSet = 900;
        probe.Milliseconds = 3000 + 27_000 - 1;
        Assert.False(gate.Check());
        probe.Milliseconds = 3000 + 27_000;
        Assert.True(gate.Check());
        Assert.Equal(2, probe.Collected);
    }

    // What TT Lab holds isn't garbage: collections that neither make room nor free a sixteenth of the budget are put off twice as long each
    // time, up to a minute, and one that frees enough brings them back to a second
    [Fact]
    public void CollectionsThatFreeLittleArePutOffLongerAndLonger()
    {
        var probe = new FakeProbe { WorkingSet = 900 };
        using var gate = Gate(probe);
        probe.Collecting = () => probe.WorkingSet -= 10;
        var collected = new List<long>();
        for (var time = 0L; time <= 260_000; time += 500)
        {
            probe.Milliseconds = time;
            probe.WorkingSet = 900;
            if (gate.Check())
            {
                collected.Add(time);
            }
        }

        Assert.Equal([0, 2000, 6000, 14_000, 30_000, 62_000, 126_000, 190_000, 254_000], collected);

        // A collection freeing a sixteenth of the budget
        probe.Milliseconds = 318_000;
        probe.Collecting = () => probe.WorkingSet -= Budget / 16;
        Assert.True(gate.Check());
        probe.Milliseconds = 319_000;
        probe.WorkingSet = 900;
        Assert.True(gate.Check());
        Assert.Equal(11, probe.Collected);
    }

    [Fact]
    public void ACollectionMakingRoomIsUseful()
    {
        var probe = new FakeProbe { WorkingSet = 820 };
        using var gate = Gate(probe);
        probe.Collecting = () => probe.WorkingSet = 790;

        Assert.True(gate.Check());
        probe.Milliseconds = 1000;
        probe.WorkingSet = 820;
        Assert.True(gate.Check());
    }
}
