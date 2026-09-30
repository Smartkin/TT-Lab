using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Avalonia.Threading;
using ReactiveUI.Avalonia;

namespace TT_Lab.Tests.Support;

// AvaloniaScheduler runs work right away when Dispatcher.UIThread says it's on its thread. Between two tests the session has reset it, and a
// thread pool thread asking gets a new one that says yes to every thread: an editor's live code check finishing after its test ran right there,
// touched its view and made Avalonia's MediaContext on that dispatcher for every later test, so no window laid out again after showing.
// Work from other threads goes to the dispatcher of the test the scheduler was made in, which drops it once that test is over.
internal sealed class TestMainThreadScheduler : LocalScheduler
{
    private readonly Thread _sessionThread = Thread.CurrentThread;
    private readonly Dispatcher _dispatcher = Dispatcher.UIThread;

    public override IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        if (Thread.CurrentThread == _sessionThread)
        {
            return AvaloniaScheduler.Instance.Schedule(state, dueTime, action);
        }

        var cancellation = new CancellationDisposable();
        var work = new CompositeDisposable(cancellation);
        _dispatcher.Post(() =>
        {
            if (!cancellation.Token.IsCancellationRequested)
            {
                work.Add(AvaloniaScheduler.Instance.Schedule(state, dueTime, action));
            }
        }, DispatcherPriority.Background);
        return work;
    }
}
