using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.Tools.Pcsx2;
using TT_Lab.ViewModels;

namespace TT_Lab.Tools.Discord;

public enum PresenceConnection
{
    Off,
    Connecting,
    Connected,
    NotConnected
}

// Shows what's done in TT Lab on the user's Discord profile while the preference is on. It looks at TT Lab every second, connects
// when the preference gets turned on, tries again 5 minutes after Discord couldn't be reached or the connection got lost, and sends
// a change at most every 4 seconds (Discord takes 5 updates in 20 seconds)
public sealed class DiscordPresence : IDisposable
{
    public const string ApplicationId = "1558190952667947058";
    public const string LogoKey = "tt_lab_logo_512";
    internal static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan UpdateSpacing = TimeSpan.FromSeconds(4);

    private readonly Func<PresenceInputs> _readInputs;
    private readonly Func<IPresenceClient> _makeClient;
    private readonly Action<Action> _toUiThread;
    private readonly Func<DateTime> _clock;
    private readonly PresenceTracker _tracker = new();
    private IPresenceClient? _client;
    private DateTime? _retryAt;
    private PresenceActivity? _sent;
    private DateTime _sentAt = DateTime.MinValue;
    private DispatcherTimer? _timer;

    public event Action? ConnectionChanged;

    public PresenceConnection Connection { get; private set; } = PresenceConnection.Off;

    public DiscordPresence(ProjectManager projects, ScenesEditorsViewModel scenes, ResourcesEditorsViewModel resources, Pcsx2Service pcsx2)
        : this(InputsOf(projects, scenes, resources, pcsx2), () => new DiscordRpcPresenceClient(ApplicationId), action => Dispatcher.UIThread.Post(action),
            () => DateTime.UtcNow)
    {
    }

    internal DiscordPresence(Func<PresenceInputs> readInputs, Func<IPresenceClient> makeClient, Action<Action> toUiThread, Func<DateTime> clock)
    {
        _readInputs = readInputs;
        _makeClient = makeClient;
        _toUiThread = toUiThread;
        _clock = clock;
    }

    internal static bool IsEnabled => Preferences.GetPreference<bool>(Preferences.DiscordRichPresence);

    public void Start()
    {
        Preferences.PreferenceChanged += OnPreferenceChanged;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
        Tick();
    }

    // Breaks are followed while it's off too, turned on during one it shows the break
    internal void Tick()
    {
        var now = _clock();
        var activity = _tracker.Follow(_readInputs(), now);
        if (!IsEnabled)
        {
            Disconnect(PresenceConnection.Off);
            return;
        }

        if (_client == null && (_retryAt == null || now >= _retryAt))
        {
            Connect();
        }

        if (Connection == PresenceConnection.Connected && _client != null && activity != _sent && now - _sentAt >= UpdateSpacing)
        {
            _client.Show(activity);
            _sent = activity;
            _sentAt = now;
        }
    }

    private void Connect()
    {
        var client = _makeClient();
        _client = client;
        _retryAt = null;
        _sent = null;
        client.Ready += () => _toUiThread(() => OnReady(client));
        client.Failed += reason => _toUiThread(() => OnFailed(client, reason));
        SetConnection(PresenceConnection.Connecting);
        Log.WriteLine("Discord Rich Presence: connecting to Discord", Log.LogType.Trace);
        try
        {
            client.Connect();
        }
        catch (Exception ex)
        {
            OnFailed(client, ex.Message);
        }
    }

    private void OnReady(IPresenceClient client)
    {
        if (client != _client)
        {
            return;
        }

        Log.WriteLine("Discord Rich Presence: connected", Log.LogType.Trace);
        _sent = null;
        _sentAt = DateTime.MinValue;
        SetConnection(PresenceConnection.Connected);
        Tick();
    }

    private void OnFailed(IPresenceClient client, string reason)
    {
        if (client != _client)
        {
            return;
        }

        Log.WriteLine($"Discord Rich Presence: {(Connection == PresenceConnection.Connected ? "lost the connection" : "couldn't connect")}, {reason}. " +
                      $"Trying again in {RetryAfter.TotalMinutes:0} minutes", Log.LogType.Trace);
        Drop();
        _retryAt = _clock() + RetryAfter;
        SetConnection(PresenceConnection.NotConnected);
    }

    private void Disconnect(PresenceConnection connection)
    {
        if (_client != null)
        {
            Log.WriteLine("Discord Rich Presence: disconnected", Log.LogType.Trace);
        }

        Drop();
        _retryAt = null;
        SetConnection(connection);
    }

    // The library's events come from its own thread, it's let go of off the UI thread and never waited for
    private void Drop()
    {
        if (_client is not { } client)
        {
            return;
        }

        _client = null;
        _sent = null;
        System.Threading.Tasks.Task.Run(client.Dispose);
    }

    private void SetConnection(PresenceConnection connection)
    {
        if (Connection == connection)
        {
            return;
        }

        Connection = connection;
        ConnectionChanged?.Invoke();
    }

    private void OnPreferenceChanged(object? sender, Preferences.PreferenceChangedArgs args)
    {
        if (args.PreferenceName == Preferences.DiscordRichPresence)
        {
            _toUiThread(() =>
            {
                _retryAt = null;
                Tick();
            });
        }
    }

    public void Dispose()
    {
        Preferences.PreferenceChanged -= OnPreferenceChanged;
        _timer?.Stop();
        Disconnect(PresenceConnection.Off);
    }

    // The editor of the panel last worked in, the other panel's when that one has none open, like the History panel follows
    private static Func<PresenceInputs> InputsOf(ProjectManager projects, ScenesEditorsViewModel scenes, ResourcesEditorsViewModel resources, Pcsx2Service pcsx2)
    {
        EditorsViewerViewModel lastUsed = scenes;
        scenes.Used += () => lastUsed = scenes;
        resources.Used += () => lastUsed = resources;
        return () =>
        {
            var phase = projects.IsCreatingProject || projects.IsOpeningProject ? ProjectPhase.Loading
                : projects.OpenedProject != null && projects.ProjectReadyAt != null ? ProjectPhase.Ready
                : ProjectPhase.None;
            IAsset? asset = null;
            if (phase == ProjectPhase.Ready)
            {
                var other = lastUsed == scenes ? (EditorsViewerViewModel)resources : scenes;
                var editor = lastUsed.ActiveEditor ?? other.ActiveEditor;
                var assets = projects.OpenedProject!.AssetManager;
                asset = editor != null && assets.DoesAssetExist(editor.EditableResource) ? assets.GetAsset(editor.EditableResource) : null;
            }

            return new PresenceInputs(phase, projects.ProjectReadyAt, asset, pcsx2.IsGameActive, HasFocus());
        };
    }

    // Any of TT Lab's windows, its floating panels and dialogues too
    private static bool HasFocus()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
               && desktop.Windows.Any(window => window.IsActive && window.WindowState != WindowState.Minimized);
    }
}
