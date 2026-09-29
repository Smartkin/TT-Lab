using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Splat;
using System.Text;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using ChunkLinks = TT_Lab.Assets.Instance.ChunkLinks;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// Plays a chunk of the open project in PCSX2 (the Play button of its scene's toolbar, settings in Preferences > Direct Game Launch)
/// and reloads it in the game when saving changed the chunks the game plays: they're built again (the build cache skips the ones
/// nothing changed in), copied into the dev folder and the game starts the chunk again. The PS2 version's releases in
/// <see cref="GameRelease"/>, and only chunks: the startup files are read once when the game boots
/// </summary>
public sealed class Pcsx2Service : IDisposable
{
    private static readonly TimeSpan SaveSettleTime = TimeSpan.FromSeconds(1);

    private readonly ProjectManager _projectManager;
    private readonly SemaphoreSlim _building = new(1, 1);
    private Pcsx2Session? _session;
    private CancellationTokenSource? _pendingReload;
    private CancellationTokenSource? _saveSettle;
    private readonly BehaviorSubject<LabURI?> _playing = new(null);

    public Pcsx2Service(ProjectManager projectManager)
    {
        _projectManager = projectManager;
        DocumentViewModel.Saved += OnDocumentSaved;
    }

    public bool IsRunning => _session?.IsRunning == true;

    /// <summary>
    /// The chunk PCSX2 plays, none once it stopped or closed
    /// </summary>
    public IObservable<LabURI?> Playing => _playing.AsObservable();

    /// <summary>
    /// Why the chunk can't be played, none when it can
    /// </summary>
    public static string? WhyNotPlayable(LevelChunk chunk)
    {
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is not TT_Lab.Project.Project project)
        {
            return "No project is open";
        }

        if (project.GetPlatform(chunk.Package) != GamePlatform.PS2)
        {
            return "Only the PS2 version's chunks play in PCSX2";
        }

        if (chunk.IsGlobalDefaultChunk || string.IsNullOrEmpty(chunk.AdditionalPath))
        {
            return "The startup chunk can't be played, open a level's chunk";
        }

        var path = GameExecutable.ChunkPath(chunk.AdditionalPath);
        return path.Length > GameExecutable.MaxChunkPathLength
            ? $"The game has room for chunk paths of up to {GameExecutable.MaxChunkPathLength} characters, {path} is longer"
            : null;
    }

    public async Task PlayAsync(LabURI chunkUri)
    {
        if (_projectManager.OpenedProject is not TT_Lab.Project.Project project || !AssetManager.Get().DoesAssetExist(chunkUri))
        {
            return;
        }

        var chunk = AssetManager.Get().GetAsset<LevelChunk>(chunkUri);
        if (WhyNotPlayable(chunk) is { } reason)
        {
            Log.WriteLine($"{chunk.Alias} can't be played: {reason}", Log.LogType.Warning);
            return;
        }

        var startChunk = GameExecutable.ChunkPath(chunk.AdditionalPath!);

        var install = await FindInstallAsync();
        var discImage = await FindDiscImageAsync(project);
        if (install == null || discImage == null)
        {
            return;
        }

        Stop();
        var folder = DevFolder(project);
        var total = Stopwatch.StartNew();
        Log.WriteLine($"Getting {chunk.Alias} ready to play in PCSX2...");
        GameRelease? release = null;
        var prepared = await RunWithProjectBusyAsync($"Error getting {chunk.Alias} ready for PCSX2", () =>
        {
            release = GameRelease.Detect(project.DiscContentPathPS2!);
            var chunks = ChunkWithLinks(chunk);
            // The patch's level select reads the project's list, where the chunks made in TT Lab are, and those only play once built
            var levelSelect = release.LevelSelectPatch != null ? LevelSelectText(project) : null;
            if (levelSelect != null)
            {
                chunks.AddRange(ChunksTheDiscLacks(project, levelSelect, Pcsx2DevFolder.ArchiveFiles(project.DiscContentPathPS2!)));
            }

            BuildChunks(project, chunks.Distinct());
            folder.WriteExecutable(release, project.DiscContentPathPS2!, startChunk);
            var extracted = folder.ExtractArchive(project.DiscContentPathPS2!);
            if (extracted > 0)
            {
                Log.WriteLine($"Took {extracted} files out of the disc's archive into {folder.FolderPath}");
            }

            folder.CopyBuiltFiles(project.GetBuildFilesPath(GamePlatform.PS2));
            folder.CopySoundBanks(project.DiscContentPathPS2!);
            if (levelSelect != null)
            {
                folder.WriteFile(LevelSelectPath, Encoding.Latin1.GetBytes(levelSelect));
            }
        });
        if (!prepared)
        {
            return;
        }

        try
        {
            var session = Pcsx2Session.Launch(install, release!, folder.ExecutablePath(release!), discImage, startChunk);
            session.Exited += () =>
            {
                Log.WriteLine("PCSX2 closed");
                if (_session == session)
                {
                    _playing.OnNext(null);
                }
            };
            _session = session;
            _playing.OnNext(chunkUri);
            Log.WriteLine($"PCSX2 is booting {chunk.Alias} ({release!.Name}), it goes straight to playing it");
            await session.EnterGameAsync(CancellationToken.None);
            Log.WriteLine($"Playing {chunk.Alias} in PCSX2 after {total.Elapsed.TotalSeconds:F0} s, saving reloads it");
        }
        catch (Exception ex)
        {
            Log.WriteLine($"PCSX2 couldn't play {chunk.Alias}: {ex.Message}", Log.LogType.Error);
            Log.WriteLine(ex.ToString(), Log.LogType.Debug);
        }
    }

    /// <summary>
    /// Builds the chunks the game plays again and has the game load them, when any of their files changed
    /// </summary>
    public async Task ReloadAsync()
    {
        var session = _session;
        if (session is not { IsRunning: true } || _projectManager.OpenedProject is not TT_Lab.Project.Project project)
        {
            Log.WriteLine("PCSX2 isn't playing a chunk, the Play button of a chunk's scene starts it", Log.LogType.Warning);
            return;
        }

        _pendingReload?.Cancel();
        var cancellation = new CancellationTokenSource();
        _pendingReload = cancellation;
        var total = Stopwatch.StartNew();
        var changed = 0;
        var built = await RunWithProjectBusyAsync("Error building the chunks PCSX2 plays", () =>
        {
            var chunks = new List<LevelChunk>();
            var start = FindChunk(project, session.StartChunk);
            if (start != null)
            {
                chunks.AddRange(ChunkWithLinks(start));
            }

            chunks.AddRange(session.LoadedChunks().Select(path => FindChunk(project, path)).OfType<LevelChunk>());
            BuildChunks(project, chunks.Distinct().ToList());
            changed = DevFolder(project).CopyBuiltFiles(project.GetBuildFilesPath(GamePlatform.PS2));
        });
        if (!built || cancellation.IsCancellationRequested)
        {
            return;
        }

        if (changed == 0)
        {
            Log.WriteLine("Nothing changed in the chunks PCSX2 plays");
            return;
        }

        try
        {
            Log.WriteLine($"Reloading the chunks in PCSX2 ({changed} files changed), it waits for the game to play if it's paused");
            await session.ReloadAsync(cancellation.Token);
            Log.WriteLine($"Reloaded the chunks in PCSX2 after {total.Elapsed.TotalSeconds:F0} s");
        }
        catch (OperationCanceledException)
        {
            // A newer reload took over
        }
        catch (Exception ex)
        {
            Log.WriteLine($"PCSX2 couldn't reload the chunks: {ex.Message}", Log.LogType.Error);
            Log.WriteLine(ex.ToString(), Log.LogType.Debug);
        }
    }

    // The game keeps running when TT Lab closes, only saving stops reloading it
    public void Dispose()
    {
        DocumentViewModel.Saved -= OnDocumentSaved;
    }

    public void Stop()
    {
        _pendingReload?.Cancel();
        if (_session == null)
        {
            return;
        }

        var session = _session;
        _session = null;
        session.Stop();
        session.Dispose();
        _playing.OnNext(null);
    }

    private void OnDocumentSaved(DocumentViewModel document)
    {
        if (!IsRunning || !Preferences.GetPreference<bool>(Preferences.Pcsx2ReloadOnSave))
        {
            return;
        }

        // Saving everything saves the documents one after another, they make one reload
        _saveSettle?.Cancel();
        var settle = new CancellationTokenSource();
        _saveSettle = settle;
        Task.Delay(SaveSettleTime, settle.Token).ContinueWith(task =>
        {
            if (!task.IsCanceled)
            {
                _ = ReloadAsync();
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async Task<bool> RunWithProjectBusyAsync(string errorMessage, Action work)
    {
        await _building.WaitAsync();
        _projectManager.WorkableProject = false;
        try
        {
            await Task.Run(work);
            return true;
        }
        catch (Exception ex)
        {
            foreach (var line in ProjectManager.BuildFailureLines(errorMessage, ex))
            {
                Log.WriteLine(line, Log.LogType.Error);
            }

            Log.WriteLine(ex.ToString(), Log.LogType.Debug);
            return false;
        }
        finally
        {
            _projectManager.WorkableProject = true;
            _building.Release();
        }
    }

    private static void BuildChunks(TT_Lab.Project.Project project, IEnumerable<LevelChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            project.PackChunk(chunk.URI);
        }
    }

    private const string LevelSelectPath = "Startup/LevelSelect.txt";

    private static string? LevelSelectText(TT_Lab.Project.Project project)
    {
        if (LevelSelect.Find(project, GamePlatform.PS2) is not { } file)
        {
            return null;
        }

        var wasLoaded = file.IsLoaded;
        var text = ((IAsset)file).GetData<TextFileData>().Text;
        if (!wasLoaded)
        {
            file.UnloadData();
        }

        return text;
    }

    // The level select's chunks whose files aren't in the disc's archive
    internal static IEnumerable<LevelChunk> ChunksTheDiscLacks(TT_Lab.Project.Project project, string levelSelect, IReadOnlySet<string> archive)
    {
        return LevelSelect.Entries(levelSelect)
            .Where(entry => !archive.Contains(Pcsx2DevFolder.GamePath($"{entry.Path}.rm2")))
            .Select(entry => FindChunk(project, entry.Path))
            .OfType<LevelChunk>();
    }

    // The game loads the start chunk with the chunks it links
    private static List<LevelChunk> ChunkWithLinks(LevelChunk chunk)
    {
        var assetManager = AssetManager.Get();
        var chunks = new List<LevelChunk> { chunk };
        var links = chunk.ChunkResources.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<ChunkLinks>().FirstOrDefault();
        if (links == null)
        {
            return chunks;
        }

        chunks.AddRange(((IAsset)links).GetData<ChunkLinksData>().Links.Select(link => link.Path).Where(assetManager.DoesAssetExist)
            .Select(assetManager.GetAsset).OfType<LevelChunk>());
        return chunks;
    }

    // The game's paths are the archive's (levels\earth\hub\beach, any case), a chunk's the folders it's in
    private static LevelChunk? FindChunk(TT_Lab.Project.Project project, string gamePath)
    {
        return AssetManager.Get().GetAllAssetsOf<LevelChunk>().FirstOrDefault(chunk => chunk.AdditionalPath != null
            && project.GetPlatform(chunk.Package) == GamePlatform.PS2
            && string.Equals(GameExecutable.ChunkPath(chunk.AdditionalPath), gamePath, StringComparison.OrdinalIgnoreCase));
    }

    private static Pcsx2DevFolder DevFolder(TT_Lab.Project.Project project) => new(Path.Combine(project.ProjectPath, "build", "pcsx2"));

    private static async Task<Pcsx2Install?> FindInstallAsync()
    {
        var install = Pcsx2Install.Find(Preferences.GetPreference<string>(Preferences.Pcsx2Path));
        if (install != null)
        {
            return install;
        }

        Log.WriteLine("PCSX2 wasn't found, pick its executable (Preferences > Direct Game Launch keeps it)", Log.LogType.Warning);
        var picked = await MiscUtils.GetFileFromDialogueAsync("Pick PCSX2's executable", "PCSX2", OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]);
        if (string.IsNullOrEmpty(picked))
        {
            return null;
        }

        Preferences.SetPreference(Preferences.Pcsx2Path, picked);
        Preferences.Save();
        return Pcsx2Install.Find(picked);
    }

    // The disc PCSX2 boots with: the game streams its videos from it (sceCdSearchFile, past the loader the executable's patched in), a
    // disc of another release plays just as well. The one picked, else the project's own
    private static async Task<string?> FindDiscImageAsync(TT_Lab.Project.Project project)
    {
        var configured = Preferences.GetPreference<string>(Preferences.Pcsx2DiscImage);
        if (File.Exists(configured))
        {
            return configured;
        }

        var built = Path.Combine(project.ProjectPath, "build", "image", $"{project.Name}.iso");
        if (File.Exists(built))
        {
            return built;
        }

        Log.WriteLine("Pick a disc image of the game for PCSX2 to boot, the project has none built yet (Preferences > Direct Game Launch keeps it)", Log.LogType.Warning);
        var picked = await MiscUtils.GetFileFromDialogueAsync("Pick a disc image of Crash Twinsanity (PS2)", "Disc images", ["*.iso", "*.bin", "*.chd", "*.cso"]);
        if (string.IsNullOrEmpty(picked))
        {
            return null;
        }

        Preferences.SetPreference(Preferences.Pcsx2DiscImage, picked);
        Preferences.Save();
        return picked;
    }
}
