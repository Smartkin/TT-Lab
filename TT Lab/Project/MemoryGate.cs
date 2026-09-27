using System;
using System.Threading;

namespace TT_Lab.Project;

/// <summary>
/// Keeps builds and project creation within TT Lab's memory budget
/// </summary>
/// <remarks>
/// Parallel work (building a chunk, reading a chunk, importing or writing an asset) allocates a lot that's garbage once it's done, which
/// the GC lets pile up far past the budget while there's memory to spare. The gate starts more work only while there's room for it and
/// collects whenever TT Lab gets over the budget. One piece of work always runs
/// </remarks>
public sealed class MemoryGate : IDisposable
{
    // A chunk can take a good part of the budget while it builds, more work only starts below this share of it
    private const double StartShare = 0.6;
    // Loading a big model allocates hundreds of megabytes at once, collecting before the budget is reached leaves room for that
    private const double CollectShare = 0.8;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CollectionInterval = TimeSpan.FromSeconds(1);

    private readonly long _budgetBytes;
    private readonly double _startShare;
    private readonly object _lock = new();
    private readonly Timer _watchdog;
    private int _running;
    private long _lastCollection;

    public MemoryGate(long budgetBytes, double startShare = StartShare)
    {
        _budgetBytes = budgetBytes;
        _startShare = startShare;
        _watchdog = new Timer(_ => CollectWhenOverBudget(), null, CheckInterval, CheckInterval);
    }

    private bool HasRoom => Environment.WorkingSet < _budgetBytes * _startShare;

    public void Enter()
    {
        lock (_lock)
        {
            while (_running > 0 && !HasRoom)
            {
                // Collections free memory without any work finishing, so the room gets checked again every now and then
                Monitor.Wait(_lock, TimeSpan.FromSeconds(1));
            }

            _running++;
        }
    }

    public void Exit()
    {
        // What the work loaded is garbage now, collecting it right away makes room for the work waiting. Small work like an asset
        // finishes thousands of times a second, a collection each time would take longer than the work
        if (!HasRoom && Environment.TickCount64 - Interlocked.Read(ref _lastCollection) >= CollectionInterval.TotalMilliseconds)
        {
            Collect();
        }

        lock (_lock)
        {
            _running--;
            Monitor.PulseAll(_lock);
        }
    }

    public void Dispose()
    {
        _watchdog.Dispose();
    }

    private void CollectWhenOverBudget()
    {
        var now = Environment.TickCount64;
        if (Environment.WorkingSet < _budgetBytes * CollectShare || now - Interlocked.Read(ref _lastCollection) < CollectionInterval.TotalMilliseconds)
        {
            return;
        }

        Collect();
    }

    private void Collect()
    {
        Interlocked.Exchange(ref _lastCollection, Environment.TickCount64);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        lock (_lock)
        {
            Monitor.PulseAll(_lock);
        }
    }
}
