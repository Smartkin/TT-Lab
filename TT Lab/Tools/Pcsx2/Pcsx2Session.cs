using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// The game TT Lab started in PCSX2 on a project's dev folder. It goes straight to playing the start chunk the way the main menu's new
/// game does: to loading it right after the logos begin, from the title to a new game, from the intro movie's state to playing before
/// the movie starts, and without the notices of the start. A reload is the game's own start of the chunk again (every chunk dropped and
/// loaded from the folder with the chunks it links) and a new game in it
/// </summary>
public sealed class Pcsx2Session : IDisposable
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(200);
    // The intro movie starts 0.6 s into its state and the notices a quarter of a second into playing, polling this fast is ahead of both
    private static readonly TimeSpan FastPoll = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan NoticesWindow = TimeSpan.FromMilliseconds(600);
    // PCSX2's PINE server waits for its client's next request and shutting the virtual machine down waits for the server: a session keeping
    // the connection without asking anything once the game played kept PCSX2 running without a window after its game window was closed,
    // and TT Lab playing. Asking for the status this often lets it close, and tells when the game stopped
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(1);
    // A PCSX2 still running this long after its game stopped (its main window back) is closed, TT Lab started it for the game
    private static readonly TimeSpan StoppedGrace = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly Pcsx2Install _install;
    private readonly GameRelease _release;
    private RunningGame? _game;
    private readonly CancellationTokenSource _watching = new();
    private int _isWatching;
    private int _hasExited;

    private Pcsx2Session(Process process, Pcsx2Install install, GameRelease release, string startChunk)
    {
        _process = process;
        _install = install;
        _release = release;
        StartChunk = startChunk;
    }

    /// <summary>
    /// The game's path of the chunk the session plays and reloads
    /// </summary>
    public string StartChunk { get; }

    public bool IsRunning => !_process.HasExited && Volatile.Read(ref _hasExited) == 0;

    /// <summary>
    /// PCSX2 closed or its game stopped, raised once
    /// </summary>
    public event Action? Exited;

    public static Pcsx2Session Launch(Pcsx2Install install, GameRelease release, string executable, string discImage, string startChunk)
    {
        install.WriteGameSettings(release);
        install.WritePatches(release);
        var process = Process.Start(install.StartInfo(executable, discImage)) ?? throw new IOException("PCSX2 didn't start");
        var session = new Pcsx2Session(process, install, release, startChunk);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => session.RaiseExited();
        return session;
    }

    /// <summary>
    /// What to ask of the game in a state on the way to playing, none to let it go on
    /// </summary>
    internal static GameState? NextOnTheWayToPlaying(GameState state, bool skipLogos) => state switch
    {
        GameState.Logos when skipLogos => GameState.LoadStartChunk,
        GameState.Title => GameState.NewGame,
        GameState.Movie => GameState.StartPlaying,
        _ => null
    };

    /// <summary>
    /// Skips the logos and the menus, done once the game plays
    /// </summary>
    public async Task EnterGameAsync(CancellationToken cancellation)
    {
        var game = await ConnectAsync(cancellation);
        await PlayAsync(game, true, cancellation);
        if (Interlocked.Exchange(ref _isWatching, 1) == 0)
        {
            _ = WatchAsync(game);
        }
    }

    private void RaiseExited()
    {
        if (Interlocked.Exchange(ref _hasExited, 1) == 0)
        {
            Exited?.Invoke();
        }
    }

    private async Task WatchAsync(RunningGame game)
    {
        var cancellation = _watching.Token;
        try
        {
            while (!_process.HasExited)
            {
                await Task.Delay(WatchInterval, cancellation);
                if (!HasStopped(game))
                {
                    continue;
                }

                RaiseExited();
                await Task.Delay(StoppedGrace, cancellation);
                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }

                return;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped or disposed
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Gone by itself in the meantime
        }
    }

    // A paused game still plays, a shut down one or a connection PCSX2 closed or refuses doesn't
    internal static bool HasStopped(RunningGame game)
    {
        try
        {
            return game.Pine.GetStatus() == PineStatus.Shutdown;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            return true;
        }
    }

    /// <summary>
    /// Waits for the game to play (a pause menu or a cutscene wait), then loads the start chunk again and plays it
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellation)
    {
        var game = await ConnectAsync(cancellation);
        while (StateOf(game) is not (GameState.Playing or GameState.Title))
        {
            await Task.Delay(Poll, cancellation);
        }

        await RequestAsync(game, GameState.LoadStartChunk, cancellation);
        await PlayAsync(game, false, cancellation);
    }

    private async Task PlayAsync(RunningGame game, bool skipLogos, CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var state = StateOf(game);
            if (state == GameState.Playing)
            {
                var playing = Stopwatch.StartNew();
                while (playing.Elapsed < NoticesWindow)
                {
                    game.SkipStartNotices();
                    await Task.Delay(FastPoll, cancellation);
                }

                return;
            }

            if (NextOnTheWayToPlaying(state, skipLogos) is { } next)
            {
                game.Request(next);
                skipLogos &= state != GameState.Logos;
            }

            var closeToPlaying = state is GameState.Title or GameState.NewGame or GameState.Movie or GameState.StartPlaying;
            await Task.Delay(closeToPlaying ? FastPoll : Poll, cancellation);
        }
    }

    public IReadOnlyList<string> LoadedChunks() => _game?.LoadedChunks() ?? [];

    // PCSX2 turns memory requests down until its virtual machine runs
    private GameState StateOf(RunningGame game)
    {
        if (_process.HasExited)
        {
            throw new IOException("PCSX2 closed");
        }

        try
        {
            return game.State;
        }
        catch (PineRefusedException)
        {
            return GameState.None;
        }
    }

    public void Stop()
    {
        _watching.Cancel();
        if (!_process.HasExited)
        {
            _process.Kill(true);
        }
    }

    // The game writes its states' word every frame, a request written in between can get lost and is written again
    private async Task RequestAsync(RunningGame game, GameState state, CancellationToken cancellation)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            game.Request(state);
            for (var wait = 0; wait < 5; wait++)
            {
                await Task.Delay(Poll, cancellation);
                if (StateOf(game) == state)
                {
                    return;
                }
            }
        }

        throw new IOException($"The game didn't go to state {state}");
    }

    private async Task<RunningGame> ConnectAsync(CancellationToken cancellation)
    {
        if (_game != null)
        {
            return _game;
        }

        var started = Stopwatch.StartNew();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (_process.HasExited)
            {
                throw new IOException("PCSX2 closed");
            }

            try
            {
                _game = new RunningGame(_install.ConnectPine(_process), _release);
                return _game;
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                if (started.Elapsed > TimeSpan.FromSeconds(60))
                {
                    throw new IOException($"PCSX2 didn't open PINE on slot {Pcsx2Install.PineSlot} ({ex.Message}), {_install.GameSettingsPath(_release)} turns it on", ex);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellation);
            }
        }
    }

    public void Dispose()
    {
        _watching.Cancel();
        _game?.Pine.Dispose();
        _process.Dispose();
    }
}
