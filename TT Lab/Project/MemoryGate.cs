using System;
using System.Threading;

namespace TT_Lab.Project;

/// <summary>
/// Keeps builds and project creation within TT Lab's memory budget
/// </summary>
/// <remarks>
/// Parallel work (building a chunk, reading a chunk, importing or writing an asset) allocates a lot that's garbage once it's done, which
/// the GC lets pile up far past the budget while there's memory to spare. The gate starts more work only while there's room for it and
/// collects whenever TT Lab gets over the budget. One piece of work always runs, whatever the working set: what TT Lab holds (a project's
/// assets, the items of a disc being unpacked) can take more than the budget, and no collection brings that down. The work running grows
/// by one piece at a time, once a piece finished or a second after it last grew: work just started hasn't allocated yet, and every chunk
/// waiting started on one reading of the working set, at a build's start and whenever a collection made room (a build at 1500 MB went to
/// 2.2 to 3.3 GB again and again). Without room the work running comes down to what runs. Collections give the memory they free
/// back to the system (a forced one kept it committed, the working set never came down and the gate collected every second for minutes).
/// One runs at a time, the next no sooner than nine times as long as the last one took after it ended, and collections that neither make
/// room nor free a sixteenth of the budget are put off twice as long each time, up to 64 s
/// </remarks>
public sealed class MemoryGate : IDisposable
{
    // What the gate reads of the process and does to it, made up by the tests
    internal interface IProbe
    {
        long WorkingSet { get; }

        long Milliseconds { get; }

        void Collect();
    }

    // A chunk can take a good part of the budget while it builds, more work only starts below this share of it
    private const double StartShare = 0.6;
    // Loading a big model allocates hundreds of megabytes at once, collecting before the budget is reached leaves room for that
    private const double CollectShare = 0.8;
    private const int CheckMilliseconds = 250;
    // Collections free memory without any work finishing, so the room gets checked again every now and then
    private const int WaitMilliseconds = 1000;
    // What a piece of work allocates shows in the working set by then, more starts beside it once it has
    private const long GrowMilliseconds = 1000;
    private const long FirstInterval = 1000;
    private const long LongestInterval = 64_000;
    // A collection stops every thread: the time after it is at least this many times as long, a tenth of the time at most goes to them
    private const long IdleFactor = 9;
    // A collection freeing this part of the budget (a sixteenth) was worth it, even when there's still no room
    private const long UsefulPart = 16;

    private readonly long _budgetBytes;
    private readonly double _startShare;
    private readonly IProbe _probe;
    private readonly object _lock = new();
    private readonly object _watchLock = new();
    private int _running;
    private int _waiting;
    // How many pieces may run: what runs whenever there's no room, one more at a time while there is
    private int _limit = 1;
    private long _lastGrowth;
    private bool _finishedSinceGrowth;
    private int _collecting;
    private bool _disposed;
    // Written by the one collection running, the next one's time read by everyone
    private long _interval = FirstInterval;
    private long _nextCollection;

    public MemoryGate(long budgetBytes, double startShare = StartShare) : this(budgetBytes, startShare, ProcessProbe.Instance, true)
    {
    }

    internal MemoryGate(long budgetBytes, double startShare, IProbe probe, bool watches)
    {
        _budgetBytes = budgetBytes;
        _startShare = startShare;
        _probe = probe;
        _nextCollection = probe.Milliseconds;
        _lastGrowth = probe.Milliseconds;
        if (watches)
        {
            // A thread of its own: the pool's timers ran while a collection took longer than their period, and hundreds of them waited in
            // collections at once while the pool, starved by the work waiting at the gate, added a thread every half a second
            new Thread(Watch) { IsBackground = true, Name = "Memory gate" }.Start();
        }
    }

    // How many waited at once at most
    internal int MostWaiting { get; private set; }

    private bool HasRoom => _probe.WorkingSet < _budgetBytes * _startShare;

    public void Enter()
    {
        lock (_lock)
        {
            if (!MayStart())
            {
                _waiting++;
                MostWaiting = Math.Max(MostWaiting, _waiting);
                try
                {
                    do
                    {
                        Monitor.Wait(_lock, WaitMilliseconds);
                    }
                    while (!MayStart());
                }
                finally
                {
                    _waiting--;
                }
            }

            _running++;
        }
    }

    public void Exit()
    {
        // What the work loaded is garbage now, collecting it right away makes room for the work waiting
        if (Volatile.Read(ref _waiting) > 0)
        {
            TryCollect(_startShare);
        }

        lock (_lock)
        {
            _running--;
            _finishedSinceGrowth = true;
            Monitor.PulseAll(_lock);
        }
    }

    public void Dispose()
    {
        lock (_watchLock)
        {
            _disposed = true;
            Monitor.PulseAll(_watchLock);
        }
    }

    // The watchdog's check: a collection when TT Lab gets close to the budget and one is due, the work waiting looks at the room again
    internal bool Check()
    {
        if (TryCollect(CollectShare))
        {
            return true;
        }

        if (Volatile.Read(ref _waiting) > 0)
        {
            lock (_lock)
            {
                Monitor.PulseAll(_lock);
            }
        }

        return false;
    }

    // Under the lock. A piece of work starting while nothing runs starts the work running over from one, a piece finishing gives its place
    // to the next while there's room, and the work running grows by one once a piece finished or a second passed since it last grew
    private bool MayStart()
    {
        if (_running == 0)
        {
            _limit = 1;
            return true;
        }

        if (!HasRoom)
        {
            _limit = _running;
            return false;
        }

        if (_running < _limit)
        {
            return true;
        }

        var now = _probe.Milliseconds;
        if (!_finishedSinceGrowth && now - _lastGrowth < GrowMilliseconds)
        {
            return false;
        }

        _limit = _running + 1;
        _lastGrowth = now;
        _finishedSinceGrowth = false;
        return true;
    }

    private void Watch()
    {
        while (true)
        {
            lock (_watchLock)
            {
                if (!_disposed)
                {
                    Monitor.Wait(_watchLock, CheckMilliseconds);
                }

                if (_disposed)
                {
                    return;
                }
            }

            try
            {
                Check();
            }
            catch (Exception exception)
            {
                Log.WriteLine($"The memory gate's check failed: {exception}", Log.LogType.Debug);
            }
        }
    }

    // A collection when the working set is past the share of the budget, none while another runs or before the next one is due
    private bool TryCollect(double share)
    {
        if (_probe.Milliseconds < Interlocked.Read(ref _nextCollection) || Interlocked.CompareExchange(ref _collecting, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            var threshold = (long)(_budgetBytes * share);
            var before = _probe.WorkingSet;
            if (before < threshold)
            {
                return false;
            }

            var started = _probe.Milliseconds;
            _probe.Collect();
            var ended = _probe.Milliseconds;
            var after = _probe.WorkingSet;
            // What TT Lab holds isn't garbage, collecting it again soon frees nothing more
            var useful = after < threshold || before - after >= _budgetBytes / UsefulPart;
            _interval = useful ? FirstInterval : Math.Min(_interval * 2, LongestInterval);
            var next = Math.Max(_interval, (ended - started) * IdleFactor);
            Interlocked.Exchange(ref _nextCollection, ended + next);
            Log.WriteLine($"The memory gate collected in {ended - started} ms: {Megabytes(before)} to {Megabytes(after)} MB of a {Megabytes(_budgetBytes)} MB budget, " +
                          $"the next collection in {next / 1000.0:0.#} s at the earliest", Log.LogType.Debug);
        }
        finally
        {
            Volatile.Write(ref _collecting, 0);
        }

        lock (_lock)
        {
            Monitor.PulseAll(_lock);
        }

        return true;
    }

    private static long Megabytes(long bytes) => bytes / (1024 * 1024);

    private sealed class ProcessProbe : IProbe
    {
        public static readonly ProcessProbe Instance = new();

        public long WorkingSet => Environment.WorkingSet;

        public long Milliseconds => Environment.TickCount64;

        // Aggressive gives what it frees back to the system: a forced collection's free regions stay committed, the working set stays as it was
        public void Collect() => GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }
}
