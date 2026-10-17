using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using TT_Lab.Assets;
using TT_Lab.Project;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// Where the Blender add-on links to TT Lab (LINK.md): a socket on the loopback, its port and a token in a discovery file of the state
/// folder that only the user can read, written again whenever another project gets opened and deleted when TT Lab quits. Every Blender
/// linking is a session of its own
/// </summary>
public sealed class BlenderLinkServer : IDisposable
{
    private readonly ProjectManager _projects;
    private readonly List<BlenderLinkSession> _sessions = [];
    private readonly CancellationTokenSource _stopping = new();
    private TcpListener? _listener;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _pushTimer;
    private string? _file;
    private string _written = string.Empty;
    private Guid? _project;
    private DateTime _lastDeletedCheck = DateTime.MinValue;

    // What TT Lab changed of what linked Blenders use, told once it stays the same for a moment (typing, dragging): by the Blender
    // whose change it was, which isn't told, the others are
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(0.3);
    private readonly Dictionary<LabURI, Pending> _pending = [];

    private sealed record Pending(IAsset Asset, DateTime Time, BlenderLinkSession? Source, bool Saved);

    public BlenderLinkServer(ProjectManager projects)
    {
        _projects = projects;
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }

    public string Token { get; }

    public int Port { get; private set; }

    public ProjectManager Projects => _projects;

    public IReadOnlyList<BlenderLinkSession> Sessions
    {
        get
        {
            lock (_sessions)
            {
                return _sessions.ToList();
            }
        }
    }

    // Folder of the discovery files, tests give one of their own
    internal string StateFolder { get; set; } = LinkProtocol.StateFolder();

    public void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        }
        catch (SocketException exception)
        {
            Log.WriteLine($"The Blender link couldn't listen: {exception.Message}", Log.LogType.Warning);
            return;
        }

        _ = AcceptAsync(_listener, _stopping.Token);
        _project = LinkedProject()?.UUID;
        WriteExecutable();
        WriteDiscovery();
        // Not of background priority: a viewport drawing all the time left the timers no turn, what TT Lab changed never reached Blender
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, _) =>
        {
            try
            {
                FollowProject();
                WriteDiscovery();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Log.WriteLine($"The Blender link couldn't follow the project: {exception.Message}", Log.LogType.Warning);
            }
        });
        _timer.Start();
        DocumentViewModel.AssetChanged += OnAssetChanged;
        DocumentViewModel.Saved += OnSaved;
        _pushTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, (_, _) => Push());
        // An exception ends a timer for good, TT Lab started without a project and nothing reached Blender after that
        _pushTimer.Start();
    }

    // Another project's assets aren't what the Blenders linked: they get told TT Lab closes and what they didn't save goes
    private void FollowProject()
    {
        var project = LinkedProject()?.UUID;
        if (project == _project)
        {
            return;
        }

        _project = project;
        _pending.Clear();
        foreach (var session in Sessions)
        {
            session.Close();
        }

        LinkedDocuments.CloseAll();
        LinkMarks.Update([]);
    }

    private void OnAssetChanged(IAsset asset) => Queue(asset, false);

    private void OnSaved(DocumentViewModel document)
    {
        if (document.DocumentModel is IAsset asset)
        {
            Queue(asset, true);
        }
    }

    private void Queue(IAsset asset, bool saved)
    {
        if (asset.URI == null || Sessions.Count == 0)
        {
            return;
        }

        var source = BlenderLinkAssets.Applying;
        if (_pending.TryGetValue(asset.URI, out var earlier))
        {
            source = earlier.Source == source ? source : null;
            saved = saved && earlier.Saved;
        }

        _pending[asset.URI] = new Pending(asset, DateTime.UtcNow, source, saved);
    }

    private void Push()
    {
        try
        {
            PushPending();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.WriteLine($"Telling Blender what TT Lab changed failed: {exception.Message}", Log.LogType.Warning);
            Log.WriteLine(exception.ToString(), Log.LogType.Debug);
        }
    }

    private void PushPending()
    {
        // No project, nothing of one to tell
        if (_projects.OpenedProject == null)
        {
            _pending.Clear();
            return;
        }

        var now = DateTime.UtcNow;
        var sessions = Sessions.Where(session => session.IsWelcomed).ToList();
        foreach (var (uri, pending) in _pending.Where(pending => now - pending.Value.Time >= Quiet).ToList())
        {
            _pending.Remove(uri);
            if (!AssetManager.Get().DoesAssetExist(uri))
            {
                continue;
            }

            foreach (var session in sessions.Where(session => session != pending.Source))
            {
                session.Assets.Tell(pending.Asset, pending.Saved);
            }
        }

        if (now - _lastDeletedCheck < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastDeletedCheck = now;
        LinkedDocuments.ForgetGone();
        foreach (var session in sessions)
        {
            session.Assets.TellDeleted();
        }

        LinkMarks.Update(Sessions);
    }

    /// <summary>
    /// Assets moved in the project, the Blenders keep them by their new URIs. What Blender changed of them and didn't save goes, their
    /// files moved
    /// </summary>
    internal void TellMoved(IReadOnlyDictionary<LabURI, LabURI> moved)
    {
        foreach (var uri in moved.Keys)
        {
            LinkedDocuments.Forget(uri);
        }

        // A model's blend file is the one of its new place
        if (_projects.OpenedProject is Project.Project project)
        {
            foreach (var (from, to) in moved)
            {
                BlendFiles.Move(project.UUID, from, to);
            }
        }

        foreach (var session in Sessions.Where(session => session.IsWelcomed))
        {
            session.Assets.TellMoved(moved);
        }
    }

    private async Task AcceptAsync(TcpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(token);
            }
            catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            client.NoDelay = true;
            var session = new BlenderLinkSession(this, client);
            lock (_sessions)
            {
                _sessions.Add(session);
            }

            _ = session.RunAsync(token);
        }
    }

    internal void Forget(BlenderLinkSession session)
    {
        lock (_sessions)
        {
            _sessions.Remove(session);
        }
    }

    internal bool Accepts(string? token)
    {
        return token != null && token.Length == Token.Length && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(token),
            System.Text.Encoding.ASCII.GetBytes(Token));
    }

    // The project the Blenders link to: not one still being read or made, whose assets aren't all there (linking a model of it was answered that
    // the project had none, and the model stayed unlinkable)
    private Project.Project? LinkedProject()
    {
        return _projects.IsOpeningProject || _projects.IsCreatingProject ? null : _projects.OpenedProject as Project.Project;
    }

    internal JsonObject? ProjectJson()
    {
        if (LinkedProject() is not { } project)
        {
            return null;
        }

        return new JsonObject { ["uuid"] = project.UUID.ToString(), ["path"] = project.ProjectPath, ["name"] = project.Name };
    }

    // The project changes now and then, the file follows it
    internal void WriteDiscovery()
    {
        if (_listener == null)
        {
            return;
        }

        var json = new JsonObject
        {
            ["protocol"] = LinkProtocol.Version,
            ["port"] = Port,
            ["token"] = Token,
            ["pid"] = Environment.ProcessId,
            ["executable"] = Environment.ProcessPath ?? string.Empty,
            ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty,
            ["addon_version"] = BundledAddonVersion(),
            ["project"] = ProjectJson()
        };
        var text = json.ToJsonString();
        if (text == _written)
        {
            return;
        }

        try
        {
            _file = Path.Combine(StateFolder, $"link-{Environment.ProcessId}.json");
            var written = _file + ".writing";
            // Only the user reads the token: the file is made that way (set after it's made, another user could open it in between)
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(StateFolder);
            }
            else
            {
                Directory.CreateDirectory(StateFolder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                // A file left by a TT Lab that crashed keeps its mode when it's opened again
                File.Delete(written);
            }

            using (var stream = new FileStream(written, options))
            {
                stream.Write(System.Text.Encoding.UTF8.GetBytes(text));
            }

            File.Move(written, _file, true);
            _written = text;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"The Blender link couldn't write {_file}: {exception.Message}", Log.LogType.Warning);
        }
    }

    /// <summary>
    /// What starts TT Lab, which Blender's Open in TT Lab runs with the project's folder when no TT Lab runs: kept after TT Lab quits
    /// </summary>
    public const string ExecutableFile = "tt-lab.json";

    private void WriteExecutable()
    {
        var process = Environment.ProcessPath ?? string.Empty;
        var command = new JsonArray(process);
        // Run through dotnet, the assembly goes after it (a single file app has no assembly files, its executable is the process)
        if (Path.GetFileNameWithoutExtension(process) == "dotnet" && Assembly.GetEntryAssembly()?.GetName().Name is { } entry)
        {
            command.Add(Path.Combine(AppContext.BaseDirectory, $"{entry}.dll"));
        }

        try
        {
            Directory.CreateDirectory(StateFolder);
            File.WriteAllText(Path.Combine(StateFolder, ExecutableFile), new JsonObject
            {
                ["command"] = command,
                ["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty
            }.ToJsonString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"Couldn't write where TT Lab is for Blender: {exception.Message}", Log.LogType.Warning);
        }
    }

    // The version in the manifest of the add-on next to TT Lab, of the zip the installer takes
    private static string BundledAddonVersion()
    {
        var zip = BlenderAddonInstaller.BundledZip();
        return zip == null ? string.Empty : Path.GetFileNameWithoutExtension(zip)["twin_tech_tools-".Length..];
    }

    /// <summary>
    /// Tells every linked Blender TT Lab quits, drops what they made or changed that wasn't saved and takes the discovery file away
    /// </summary>
    public void Stop()
    {
        _timer?.Stop();
        _pushTimer?.Stop();
        DocumentViewModel.AssetChanged -= OnAssetChanged;
        DocumentViewModel.Saved -= OnSaved;
        foreach (var session in Sessions)
        {
            session.Close();
        }

        _stopping.Cancel();
        _listener?.Stop();
        _listener = null;
        try
        {
            if (_file != null && File.Exists(_file))
            {
                File.Delete(_file);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"The Blender link couldn't delete {_file}: {exception.Message}", Log.LogType.Warning);
        }
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }
}
