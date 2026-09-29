using System;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.ViewModels;

// Playing the chunk in PCSX2 from the toolbar (Tools/Pcsx2): Play boots the game straight into it, Reload builds the chunks the game
// has again and reloads them in it, Stop closes PCSX2. What PCSX2 and disc image it uses is in Preferences > Direct Game Launch
public partial class ViewportViewModel
{
    private const string PlayInGameDefaultHint = "Play the chunk in PCSX2, in a window of its own: its chunk and the ones it links get built and the game boots straight into it";

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canPlayInGame;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _playInGameHint = PlayInGameDefaultHint;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isGameRunning;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isPlayingThisChunk;

    private LabURI? _playingChunk;

    public ReactiveCommand<Unit, Unit> PlayInGameCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ReloadInGameCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> StopGameCommand { get; private set; } = null!;

    private void InitGameLaunch()
    {
        var game = Locator.Current.GetService<Pcsx2Service>();
        // A command raises CanExecuteChanged on the thread its condition changed on and the button updates itself from it: tabs set their
        // viewport up on the task pool, and opening a chunk failed with a call from an invalid thread
        var canPlay = this.WhenAnyValue(x => x.CanPlayInGame).ObserveOn(RxSchedulers.MainThreadScheduler);
        var running = this.WhenAnyValue(x => x.IsGameRunning).ObserveOn(RxSchedulers.MainThreadScheduler);
        PlayInGameCommand = ReactiveCommand.CreateFromTask(() => game!.PlayAsync(((LevelChunk)_document!.DocumentModel).URI), canPlay);
        ReloadInGameCommand = ReactiveCommand.CreateFromTask(() => game!.ReloadAsync(), running);
        StopGameCommand = ReactiveCommand.Create(() => game!.Stop(), running);
        game?.Playing.ObserveOn(RxSchedulers.MainThreadScheduler).Subscribe(chunk =>
        {
            _playingChunk = chunk;
            IsGameRunning = chunk != null;
            IsPlayingThisChunk = chunk != null && _document?.DocumentModel is LevelChunk shown && shown.URI == chunk;
        }).DisposeWith(_closeDisposables);
    }

    private void FollowGameLaunch(DocumentViewModel document)
    {
        if (document.DocumentModel is not LevelChunk chunk)
        {
            CanPlayInGame = false;
            return;
        }

        var reason = Locator.Current.GetService<Pcsx2Service>() == null ? "Playing in PCSX2 isn't available" : Pcsx2Service.WhyNotPlayable(chunk);
        CanPlayInGame = reason == null;
        PlayInGameHint = reason ?? PlayInGameDefaultHint;
        IsPlayingThisChunk = _playingChunk != null && _playingChunk == chunk.URI;
    }
}
