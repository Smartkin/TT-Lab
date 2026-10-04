using Caliburn.Micro;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetResolvers;
using TT_Lab.Assets;
using TT_Lab.Project.Build;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Libraries;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Path = TT_Lab.Assets.Instance.Path;
using ReactiveUI;

namespace TT_Lab.Project;

/// <summary>
/// Core project class
/// </summary>
public class Project : IProject
{
    // 1.1.0 keeps sceneries' meshes and LODs as a list, the build makes the tree the game culls them with
    internal const string CURRENT_VERSION = "1.1.0";

    public AssetManager AssetManager { get; private set; }

    public Package BasePackage { get; private set; }

    public Package GlobalPackagePS2 { get; private set; }

    public Package GlobalPackageXbox { get; private set; }

    public Package Ps2Package { get; private set; }

    public Package XboxPackage { get; private set; }

    public Guid UUID { get; }

    public string Name { get; set; }

    public string Path { get; set; }

    public string? DiscContentPathPS2 { get; set; }

    public string? DiscContentPathXbox { get; set; }

    public DateTime LastModified { get; set; }

    public string Version { get; private set; } = CURRENT_VERSION;

    public string ProjectPath => System.IO.Path.Combine(Path, Name);

    public Project()
    {
        LastModified = DateTime.Now;
        UUID = Guid.NewGuid();
        AssetManager = new();
    }

    public Project(string name, string path, string? discContentPathPS2, string? discContentPathXbox) : this()
    {
        Name = name;
        Path = path;
        DiscContentPathPS2 = discContentPathPS2;
        DiscContentPathXbox = discContentPathXbox;
    }

    public void CreateProjectStructure()
    {
        System.IO.Directory.CreateDirectory(ProjectPath);
        System.IO.Directory.SetCurrentDirectory(ProjectPath);
        System.IO.Directory.CreateDirectory("assets");
        System.IO.Directory.CreateDirectory("disc");
    }

    public void Serialize(Func<IAsset, bool>? isWritten = null)
    {
        var path = ProjectPath;

        // Update last modified date
        LastModified = DateTime.Now;

        System.IO.Directory.SetCurrentDirectory(path);
        using (System.IO.FileStream fs = new(Name + ".tson", System.IO.FileMode.Create, System.IO.FileAccess.Write))
        using (System.IO.BinaryWriter writer = new(fs))
        {
            writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented).ToCharArray());
        }
        // Serialize all the assets, every asset writes to its own absolute path so they can all be written in parallel
        System.IO.Directory.SetCurrentDirectory("assets");
        var assetsToSerialize = AssetManager.GetAssets().Where(asset => !asset.IsInternal && isWritten?.Invoke(asset) != true).ToList();
        var startAsset = DateTime.Now;
        Parallel.ForEach(assetsToSerialize, asset =>
        {
#if !DEBUG
            try
            {
#endif
            asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
#if !DEBUG
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Error serializing {asset.Name}: {ex.Message}");
            }
#endif
        });

        Log.WriteLine($"Serialized assets in {(DateTime.Now - startAsset)}");
        
        // Data is kept until everything is written since exporting an asset can read the data of the ones it references
        var startUnload = DateTime.Now;
        Log.WriteLine($"Unloading all the loaded data...");
        Parallel.ForEach(assetsToSerialize, asset => asset.Serialize(SerializationFlags.FixReferences));

        // Internal assets were either temporary import assets that are merged into their owners by now or were recreated by loading
        // their owners above. They're never serialized so a reopened project doesn't have them either
        var internalAssets = AssetManager.GetAssets().Where(asset => asset.IsInternal).ToList();
        foreach (var internalAsset in internalAssets)
        {
            AssetManager.RemoveAsset(internalAsset);
        }
        Log.WriteLine($"Finished unloading the data in {(DateTime.Now - startUnload)}, removed {internalAssets.Count} internal assets");
        
        Log.WriteLine("Deleting empty folders...");
        System.IO.Directory.SetCurrentDirectory(path);
        System.IO.Directory.SetCurrentDirectory("assets");
        var dirInfo = new System.IO.DirectoryInfo($"{path}/assets");
        foreach (var packageDirectory in dirInfo.GetDirectories())
        {
            DeleteEmptyFolders(packageDirectory);
        }

        Log.WriteLine("Finished deleting empty folders...");
        
        System.IO.Directory.SetCurrentDirectory(path);
    }

    // Folders of the kinds of assets that ended up internal (models, skins and meshes are kept in the files of what they're parts of) are
    // left empty. Their folders go first, a folder that only had empty folders is empty then too. Packages' folders have their package's file
    internal static void DeleteEmptyFolders(System.IO.DirectoryInfo directory)
    {
        foreach (var child in directory.GetDirectories())
        {
            DeleteEmptyFolders(child);
            if (!child.EnumerateFileSystemInfos().Any())
            {
                child.Delete();
            }
        }
    }

    public static void Deserialize(string projectPath)
    {
        Project? pr;
        using (System.IO.FileStream fs = new(projectPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
        using (System.IO.BinaryReader reader = new(fs))
        {
            var prText = new string(reader.ReadChars((Int32)fs.Length));
            try
            {
                pr = JsonConvert.DeserializeObject<Project>(prText);
            }
            catch (JsonException ex)
            {
                throw new ProjectException($"Failed to read the project file: {ex.Message}", ex);
            }
        }
        if (pr == null)
        {
            throw new ProjectException("Failed to deserialize the project!");
        }
        if (pr.Version != CURRENT_VERSION)
        {
            throw new ProjectException($"The project was made with project version {pr.Version} but this version of TT Lab only opens version {CURRENT_VERSION}. " +
                                       "Create the project again from the game's files to use it with this version.");
        }
        System.IO.Directory.SetCurrentDirectory(System.IO.Path.GetDirectoryName(projectPath)!);
        Locator.Current.GetService<ProjectManager>()!.OpenedProject = pr;

        // Let's not kill user's CPUs here
        const int taskLimit = 4;
        var taskList = new List<Task<Dictionary<LabURI, IAsset>>>();
        var completedTasks = new List<Task<Dictionary<LabURI, IAsset>>>();
        // Deserialize assets
        foreach (var dir in System.IO.Directory.GetDirectories("assets"))
        {
            Log.WriteLine($"Opening {dir}...");
            var assetFiles = System.IO.Directory.GetFiles(dir, "*.json", System.IO.SearchOption.AllDirectories);
            taskList.Add(AssetDeserializerFactory.GetAssets(assetFiles));
            if (taskList.Count < taskLimit)
            {
                continue;
            }
            
            Task.WaitAll(taskList.Cast<Task>().ToArray());
            completedTasks.AddRange(taskList);
            taskList.Clear();
        }
        
        Task.WaitAll(taskList.Cast<Task>().ToArray());
        completedTasks.AddRange(taskList);
        taskList.Clear();
        
        foreach (var task in completedTasks.ToArray())
        {
            task.Dispose();
        }
        Log.WriteLine("Finished opening assets...");
        Dictionary<LabURI, IAsset> assets = new();
        pr.AssetManager = new();
        foreach (var assetsList in completedTasks)
        {
            foreach (var asset in assetsList.Result)
            {
                if (!assets.TryAdd(asset.Key, asset.Value))
                {
                    Log.WriteLine($"{asset.Key} is in two folders of the project's assets, {asset.Value.Name} of the second is left out", Log.LogType.Warning);
                }
            }
        }
        pr.AssetManager.AddAllAssets(assets);
        Log.WriteLine("Post processing the assets...");
        foreach (var asset in assets)
        {
            asset.Value.PostDeserialize();
        }
        pr.BasePackage = (Package)assets.Values.First(a => a.Name == pr.Name);
        pr.GlobalPackagePS2 = (Package)assets.Values.First(a => a.Name == $"Global PS2_{pr.Name}");
        pr.GlobalPackageXbox = (Package)assets.Values.First(a => a.Name == $"Global XBOX_{pr.Name}");
        pr.Ps2Package = (Package)assets.Values.First(a => a.Name == $"PS2_{pr.Name}");
        pr.XboxPackage = (Package)assets.Values.First(a => a.Name == $"XBOX_{pr.Name}");
    }

    public void CopyDiscContents()
    {
        System.IO.Directory.SetCurrentDirectory("disc");
            
        if (!string.IsNullOrEmpty(DiscContentPathPS2))
        {
            System.IO.Directory.CreateDirectory("ps2");
            System.IO.Directory.SetCurrentDirectory("ps2");
            foreach (var dirPath in System.IO.Directory.GetDirectories(DiscContentPathPS2, "*", System.IO.SearchOption.AllDirectories))
            {
                if (System.IO.Directory.Exists(dirPath.Replace(DiscContentPathPS2, "")))
                {
                    continue;
                }
                
                System.IO.Directory.CreateDirectory(dirPath.Replace(DiscContentPathPS2, ""));
            }

            foreach (var newPath in System.IO.Directory.GetFiles(DiscContentPathPS2, "*.*", System.IO.SearchOption.AllDirectories))
            {
                System.IO.File.Copy(newPath, newPath.Replace(DiscContentPathPS2, ""), true);
            }

            DiscContentPathPS2 = $"{ProjectPath}/disc/ps2";
                
            System.IO.Directory.SetCurrentDirectory("../");
        }

        if (!string.IsNullOrEmpty(DiscContentPathXbox))
        {
            System.IO.Directory.CreateDirectory("xbox");
            System.IO.Directory.SetCurrentDirectory("xbox");
            foreach (var dirPath in System.IO.Directory.GetDirectories(DiscContentPathXbox, "*", System.IO.SearchOption.AllDirectories))
            {
                if (System.IO.Directory.Exists(dirPath.Replace(DiscContentPathXbox + System.IO.Path.DirectorySeparatorChar, "")))
                {
                    continue;
                }
                    
                System.IO.Directory.CreateDirectory(dirPath.Replace(DiscContentPathXbox + System.IO.Path.DirectorySeparatorChar, ""));
            }

            foreach (var newPath in System.IO.Directory.GetFiles(DiscContentPathXbox, "*.*", System.IO.SearchOption.AllDirectories))
            {
                System.IO.File.Copy(newPath, newPath.Replace(DiscContentPathXbox + System.IO.Path.DirectorySeparatorChar, ""), true);
            }
                
            DiscContentPathXbox = $"{ProjectPath}/disc/xbox";
                
            System.IO.Directory.SetCurrentDirectory("../");
        }
            
        System.IO.Directory.SetCurrentDirectory("../");
    }

    public void CreateBasePackages()
    {
        BasePackage = new Package(Name);
        BasePackage.RegenerateLinks();
        AssetManager.AddAsset(BasePackage);
        GlobalPackagePS2 = new Package("Global PS2", Name);
        GlobalPackagePS2.RegenerateLinks();
        GlobalPackageXbox = new Package("Global XBOX", Name)
        {
            Enabled = false
        };
        GlobalPackageXbox.RegenerateLinks();
        Ps2Package = new Package("PS2", Name);
        Ps2Package.RegenerateLinks();
        Ps2Package.AddDependency(GlobalPackagePS2.URI);
        XboxPackage = new Package("XBOX", Name)
        {
            Enabled = false
        };
        XboxPackage.RegenerateLinks();
        XboxPackage.AddDependency(GlobalPackageXbox.URI);
        AssetManager.AddAsset(GlobalPackagePS2);
        AssetManager.AddAsset(GlobalPackageXbox);
        AssetManager.AddAsset(Ps2Package);
        AssetManager.AddAsset(XboxPackage);
        BasePackage.AddDependency(GlobalPackagePS2.URI);
        BasePackage.AddDependency(GlobalPackageXbox.URI);
        BasePackage.AddDependency(Ps2Package.URI);
        BasePackage.AddDependency(XboxPackage.URI);
    }

    public void UnpackAssetsPS2(MemoryGate gate)
    {
        if (string.IsNullOrEmpty(DiscContentPathPS2))
        {
            Log.WriteLine("No PS2 assets provided, skipped...");
            return;
        }

        var archivePath = System.IO.Directory.GetFiles(System.IO.Path.Combine(DiscContentPathPS2, "Crash6"), "*.BD", System.IO.SearchOption.TopDirectoryOnly)[0];
        Log.WriteLine("Reading game archives...");
        // Files are read from the archive when they're needed, all of it at once takes as much memory as the disc
        var records = PS2BD.ReadRecords(archivePath.Replace(".BD", ".BH"));
        UnpackAssets(GamePlatform.PS2, records.Select(record => new DiscFile(record.Path, () => PS2BD.ReadFile(archivePath, record))).ToList(), GlobalPackagePS2, Ps2Package, gate);
    }

    public void UnpackAssetsXbox(MemoryGate gate)
    {
        if (string.IsNullOrEmpty(DiscContentPathXbox))
        {
            Log.WriteLine("No XBox assets provided, skipped...");
            return;
        }

        GlobalPackageXbox.Enabled = true;
        XboxPackage.Enabled = true;
        UnpackAssets(GamePlatform.Xbox, GetXboxDiscFiles(DiscContentPathXbox), GlobalPackageXbox, XboxPackage, gate);
    }

    /// <summary>
    /// The Xbox version keeps its files loose on the disc, the folders the PS2 version packs into its archive hold the game's data
    /// </summary>
    internal static List<DiscFile> GetXboxDiscFiles(string discPath)
    {
        return XboxDataFolders.Select(folder => System.IO.Path.Combine(discPath, folder))
            .Where(System.IO.Directory.Exists)
            .SelectMany(folder => System.IO.Directory.EnumerateFiles(folder, "*", System.IO.SearchOption.AllDirectories))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .Select(file => new DiscFile(System.IO.Path.GetRelativePath(discPath, file).Replace(System.IO.Path.DirectorySeparatorChar, '\\'),
                () => System.IO.File.ReadAllBytes(file)))
            .ToList();
    }

    private static readonly String[] XboxDataFolders = ["Startup", "Levels", "Extras", "Language"];

    internal readonly record struct DiscFile(string Path, Func<Byte[]> Read);

    public enum GamePlatform
    {
        PS2,
        Xbox
    }

    private void UnpackAssets(GamePlatform platform, List<DiscFile> files, Package globalPackage, Package platformPackage, MemoryGate gate)
    {
        ResolverManager.Start();

        Dictionary<LabURI, IAsset> assets = new();
        var (resourceExtension, sceneryExtension) = platform == GamePlatform.PS2 ? (".rm2", ".sm2") : (".rmx", ".smx");

        // Maps graph ID to behaviour starter. Chunks are read for their starters in parallel and dropped, all of the game's chunks read
        // at once take gigabytes. Resolving below reads them again one after another
        Log.WriteLine("Creating behaviour starter map...");
        var resourceFiles = files.Where(file => file.Path.ToLower().EndsWith(resourceExtension)).ToList();
        var chunkStarters = new List<TwinBehaviourStarter>[resourceFiles.Count];
        Parallel.For(0, resourceFiles.Count, i =>
        {
            gate.Enter();
            try
            {
                chunkStarters[i] = GetStarters(ReadChunk(platform, resourceFiles[i].Path, resourceFiles[i].Read()));
            }
            finally
            {
                gate.Exit();
            }
        });

        var starterMap = new Dictionary<string, TwinBehaviourStarter>();
        for (var i = 0; i < resourceFiles.Count; i++)
        {
            var pathLow = resourceFiles[i].Path.ToLower();
            foreach (var starter in chunkStarters[i])
            {
                var starterStr = (starter.Assigners[0].Behaviour - 1).ToString();
                if (starterMap.ContainsKey(starterStr))
                {
                    starterStr += pathLow;
                }
                starterMap.Add(starterStr, starter);
            }
        }

        Log.WriteLine("Reading chunks...");
        var chunks = new ChunkReader(platform, files.Where(file => IsChunkPath(file.Path, resourceExtension, sceneryExtension)).ToList(), gate);

        var gameObjectResolver = new GameObjectResolver(starterMap);
        var behaviourResolver = new BehaviourResolver(starterMap);
        var behaviourSequenceResolver = new BehaviourSequenceResolver();
        var skydomeResolver = new SkydomeResolver();
        
        var chunkResolvers = new List<IAssetResolver>();

        // Unpack all assets from chunks
        foreach (var file in files)
        {
            var path = file.Path.Replace('\\', System.IO.Path.DirectorySeparatorChar);
            var pathLow = path.ToLower();
            var isRm = pathLow.EndsWith(resourceExtension);
            var isSm = pathLow.EndsWith(sceneryExtension);
            var isDefault = pathLow.EndsWith("default" + resourceExtension);
            var isTxt = pathLow.EndsWith(".txt");
            var isFrontend = pathLow.EndsWith("frontend.bin");
            var isPsm = pathLow.EndsWith(".psm");
            var isFont = pathLow.EndsWith(".psf");
            var isPtc = pathLow.EndsWith(".ptc");
            var isIco = pathLow.EndsWith(".ico");
            if (isTxt || isFont || isPsm || isPtc || isFrontend || isIco)
            {
                Log.WriteLine($"Unpacking {System.IO.Path.GetFileName(pathLow)}...");
                var data = file.Read();
                using System.IO.MemoryStream ms = new(data);
                var resourceName = System.IO.Path.GetFileName(path)[..^4];
                path = path[..^4];
                var otherFolders = path.Split(System.IO.Path.DirectorySeparatorChar);
                var resourcePath = string.Join(System.IO.Path.DirectorySeparatorChar, otherFolders[..^1]);

                // Check for text files
                if (isTxt)
                {
                    using System.IO.BinaryReader textReader = new(ms, Encoding.Latin1);
                    var text = textReader.ReadChars((int)ms.Length);
                    var textFile = new TextFile(globalPackage.URI, true, pathLow, resourceName, new String(text))
                    {
                        GlobalPath = resourcePath
                    };
                    textFile.RegenerateLinks();
                    assets.Add(textFile.URI, textFile);
                    continue;
                }

                using System.IO.BinaryReader globalReader = new(ms);

                // Check for fonts
                if (isFont)
                {
                    ITwinPSF font = platform == GamePlatform.PS2 ? new PS2PSF() : new XboxPSF();
                    font.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var fontAsset = new Font(globalPackage.URI, true, pathLow, resourceName, font)
                    {
                        GlobalPath = resourcePath
                    };
                    fontAsset.RegenerateLinks();
                    assets.Add(fontAsset.URI, fontAsset);
                    continue;
                }

                // Check for PSM
                if (isPsm)
                {
                    ITwinPSM psm = platform == GamePlatform.PS2 ? new PS2PSM() : new XboxPSM();
                    psm.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var psmAsset = new PSM(globalPackage.URI, true, pathLow, resourceName, psm)
                    {
                        GlobalPath = resourcePath
                    };
                    psmAsset.RegenerateLinks();
                    assets.Add(psmAsset.URI, psmAsset);
                    continue;
                }

                // Check for PTC
                if (isPtc)
                {
                    ITwinPTC ptc = platform == GamePlatform.PS2 ? new PS2PTC() : new XboxPTC();
                    ptc.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var ptcAsset = new PTC(globalPackage.URI, true, pathLow, resourceName, ptc)
                    {
                        GlobalPath = resourcePath
                    };
                    ptcAsset.RegenerateLinks();
                    assets.Add(ptcAsset.URI, ptcAsset);
                    continue;
                }

                // Check for Save Icon
                if (isIco)
                {
                    var ico = new SaveIcon(globalPackage.URI, false, "", resourceName, data)
                    {
                        GlobalPath = resourcePath
                    };
                    ico.RegenerateLinks();
                    assets.Add(ico.URI, ico);
                    continue;
                }

                // Check for frontend (UI sound effects library)
                if (isFrontend)
                {
                    ITwinSection frontend = platform == GamePlatform.PS2 ? new PS2Frontend() : new XboxFrontend();
                    frontend.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var uiLibrary = new UiSoundLibrary(globalPackage.URI, false, "", "Frontend", frontend)
                    {
                        Alias = "UI Sound Library",
                        GlobalPath = resourcePath
                    };
                    uiLibrary.RegenerateLinks();
                    assets.Add(uiLibrary.URI, uiLibrary);
                    continue;
                }
            }

            // Check for chunk file
            if (!isRm && !isSm)
            {
                continue;
            }

            Log.WriteLine($"Unpacking {System.IO.Path.GetFileName(pathLow)}...");
            var chunk = chunks.Take(file.Path);
            IAssetResolver? assetResolver = null;
            if (isDefault)
            {
                assetResolver = new DefaultChunkResolver(gameObjectResolver, behaviourResolver, behaviourSequenceResolver);
            }
            else if (isRm)
            {
                assetResolver = new ResourceChunkResolver(gameObjectResolver, behaviourResolver, behaviourSequenceResolver);
            }
            else if (isSm)
            {
                assetResolver = new SceneryChunkResolver(skydomeResolver);
            }

            ResolverManager.PerformResolve(isDefault ? globalPackage : platformPackage, pathLow[..^4], chunk, assetResolver!);

            // For default, we just gonna dump everything instantly because it can't cross-reference resources
            if (isDefault)
            {
                assetResolver!.FinalizeResolve();
            }
            else
            {
                chunkResolvers.Add(assetResolver!);
            }
        }

        Log.WriteLine("Adding unpacked assets into asset manager...");
        skydomeResolver.FinalizeResolve();
        
        foreach (var chunkResolver in chunkResolvers)
        {
            chunkResolver.FinalizeResolve();
        }
        
        behaviourSequenceResolver.FinalizeResolve();
        behaviourResolver.FinalizeResolve();
        gameObjectResolver.FinalizeResolve();
        
        AssetManager.AddAllAssets(assets);
        
        ResolverManager.Stop();
    }

    private static List<TwinBehaviourStarter> GetStarters(ITwinSection chunk)
    {
        var starters = new List<TwinBehaviourStarter>();
        var behaviours = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            var behaviour = behaviours.GetItem<TwinBehaviourWrapper>(behaviours.GetItem(i).GetID());
            if (behaviour.GetID() % 2 == 0)
            {
                starters.Add((TwinBehaviourStarter)behaviour);
            }
        }

        return starters;
    }

    // Reads the chunks in the order they get resolved, a few of them ahead in the background, and lets go of each once it's taken
    private sealed class ChunkReader(GamePlatform platform, List<DiscFile> files, MemoryGate gate)
    {
        private readonly int _ahead = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        private readonly Task<ITwinSection>?[] _reads = new Task<ITwinSection>?[files.Count];
        private int _started;

        public ITwinSection Take(string path)
        {
            var index = files.FindIndex(file => file.Path == path);
            while (_started < files.Count && _started <= index + _ahead)
            {
                var file = files[_started];
                _reads[_started] = Task.Run(() =>
                {
                    gate.Enter();
                    try
                    {
                        return ReadChunk(platform, file.Path, file.Read());
                    }
                    finally
                    {
                        gate.Exit();
                    }
                });
                _started++;
            }

            var chunk = _reads[index]!.Result;
            _reads[index] = null;
            return chunk;
        }
    }

    private static bool IsChunkPath(string path, string resourceExtension, string sceneryExtension)
    {
        var pathLow = path.ToLower();
        return pathLow.EndsWith(resourceExtension) || pathLow.EndsWith(sceneryExtension);
    }

    private static ITwinSection ReadChunk(GamePlatform platform, string path, Byte[] data)
    {
        var pathLow = path.ToLower();
        ITwinSection chunk = platform switch
        {
            GamePlatform.PS2 => pathLow.EndsWith("default.rm2") ? new PS2Default() : pathLow.EndsWith(".rm2") ? new PS2AnyTwinsanityRM2() : new PS2AnyTwinsanitySM2(),
            _ => pathLow.EndsWith("default.rmx") ? new XboxDefault() : pathLow.EndsWith(".rmx") ? new XboxAnyTwinsanityRMX() : new XboxAnyTwinsanitySMX()
        };
        using var ms = new System.IO.MemoryStream(data);
        using var reader = new System.IO.BinaryReader(ms);
        chunk.Read(reader, (Int32)ms.Length);
        return chunk;
    }

    public void PackChunk(LabURI chunkUri, ITwinItemFactory? itemFactory = null)
    {
        var chunk = AssetManager.GetAsset<LevelChunk>(chunkUri);
        var platform = GetPlatform(chunk.Package);
        var factory = itemFactory ?? CreateFactory(platform);
        factory.GlobalPackage = GetGlobalPackage(platform);
        var cache = BuildCache.Load(ProjectPath, AssetManager);

        var archivesPath = GetBuildFilesPath(platform);
        if (chunk.Name == "default")
        {
            BuildChunk(factory, cache, chunk, System.IO.Path.Combine(archivesPath, "Startup"), true, platform);
        }
        else
        {
            // The first folder of a chunk's path is the levels folder and the last one is the chunk's own
            var levelFolders = chunk.AdditionalPath!.Split(System.IO.Path.DirectorySeparatorChar).Skip(1).SkipLast(1);
            BuildChunk(factory, cache, chunk, System.IO.Path.Combine([archivesPath, "Levels", ..levelFolders]), false, platform);
        }

        cache.Save();
    }

    // Packages of the Xbox version depend on its global package, every other one is the PS2 version's
    internal GamePlatform GetPlatform(LabURI package)
    {
        var xbox = package == GlobalPackageXbox.URI || package == XboxPackage.URI;
        return xbox || (AssetManager.IsRelated(package, GlobalPackageXbox.URI) && !AssetManager.IsRelated(package, GlobalPackagePS2.URI))
            ? GamePlatform.Xbox
            : GamePlatform.PS2;
    }

    /// <summary>
    /// Behaviour commands of the version of the game the asset belongs to
    /// </summary>
    public static string GetActionDefinitionsFile(IAsset? asset)
    {
        var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject as Project;
        return asset != null && project != null && project.GetPlatform(asset.Package) == GamePlatform.Xbox ? "ActionDefinitionsXbox.lab" : "ActionDefinitionsPs2.lab";
    }

    private Package GetGlobalPackage(GamePlatform platform) => platform == GamePlatform.Xbox ? GlobalPackageXbox : GlobalPackagePS2;

    private ITwinItemFactory CreateFactory(GamePlatform platform)
    {
        return platform == GamePlatform.Xbox ? new XboxItemFactory { GlobalPackage = GlobalPackageXbox } : new PS2ItemFactory { GlobalPackage = GlobalPackagePS2 };
    }

    // The PS2 version's files get packed into its archive, the Xbox version's are written as they're on the disc
    internal string GetBuildFilesPath(GamePlatform platform)
    {
        return platform == GamePlatform.Xbox
            ? System.IO.Path.Combine(ProjectPath, "build", "xbox", "files")
            : System.IO.Path.Combine(ProjectPath, "build", "archives");
    }

    private static string[] GetChunkOutputs(LevelChunk chunk, string outputDirectory, bool isDefault, GamePlatform platform)
    {
        var (resourceExtension, sceneryExtension) = platform == GamePlatform.PS2 ? (".rm2", ".sm2") : (".rmx", ".smx");
        var rm2Path = System.IO.Path.Combine(outputDirectory, isDefault ? StringExtensions.CapitalizeFirstChar($"{chunk.Name}{resourceExtension}") : $"{chunk.Name}{resourceExtension}");
        return isDefault ? [rm2Path] : [rm2Path, System.IO.Path.Combine(outputDirectory, $"{chunk.Name}{sceneryExtension}")];
    }

    // Keeps the chunk's archives from a previous build when nothing they were made from changed
    private void BuildChunk(ITwinItemFactory buildFactory, BuildCache cache, LevelChunk chunk, string outputDirectory, bool isDefault, GamePlatform platform)
    {
        var outputs = GetChunkOutputs(chunk, outputDirectory, isDefault, platform);
        var cacheKey = $"chunk:{chunk.URI}";
        if (cache.IsUpToDate(cacheKey, outputs))
        {
            Log.WriteLine($"{chunk.Alias} didn't change since the last build, keeping it");
            return;
        }

        WriteChunk(buildFactory, cache, chunk, outputs, isDefault);
    }

    // Chunks build in parallel. Each gets a factory of its own, which keeps track of what went into the chunk, and a scope for
    // the asset data it loads, which is released once the chunk is written
    internal void WriteChunk(ITwinItemFactory buildFactory, BuildCache cache, LevelChunk chunk, string[] outputs, bool isDefault)
    {
        try
        {
            WriteChunkFiles(buildFactory, cache, chunk, outputs, isDefault);
        }
        catch (Exception ex) when (ex is not BuildException)
        {
            throw new BuildException(chunk.Alias, ex);
        }
    }

    private void WriteChunkFiles(ITwinItemFactory buildFactory, BuildCache cache, LevelChunk chunk, string[] outputs, bool isDefault)
    {
        Log.WriteLine($"Writing {chunk.Alias}...");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputs[0])!);
        var factory = buildFactory.ForChunk();
        factory.ChunkPath = chunk.AdditionalPath!;
        factory.IsDefaultResolution = isDefault;
        var accessedAssets = new System.Collections.Concurrent.ConcurrentDictionary<IAsset, byte>();
        accessedAssets.TryAdd(chunk, 0);
        using (AssetManager.RecordAccessedAssets(accessedAssets))
        using (new AssetDataScope())
        {
            // References to objects and sounds that differ between chunks get this chunk's versions
            factory.ChunkVersions = chunk.ItemVersions.Where(AssetManager.DoesAssetExist).Select(AssetManager.GetAsset)
                .GroupBy(asset => (asset.GetType(), asset.ID)).ToDictionary(group => group.Key, group => group.First().URI);
            factory.Overrides = new ChunkOverrides(chunk.Overrides);
            using var overrides = factory.Overrides.Use();
            using var layoutIndexes = new LayoutIndexes(chunk.Alias, chunk.ChunkResources.Select(AssetManager.GetAsset)).Use();
            var rm2 = isDefault ? factory.GenerateDefault() : factory.GenerateRM();
            var sm2 = factory.GenerateSM();
            foreach (var asset in chunk.ChunkResources.Select(AssetManager.GetAsset))
            {
                Log.WriteLine($"Writing {asset.Name} of {chunk.Alias}...", Log.LogType.Debug);
                try
                {
                    ResolveChunkResource(factory, chunk, asset, rm2, sm2, isDefault);
                }
                catch (Exception ex) when (ex is not BuildException)
                {
                    throw new BuildException($"{asset.Name} of {chunk.Alias}", ex);
                }
            }

            ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_COLLISION_ITEM, 2);
            ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_PARTICLES_ITEM, 2);
            // Written before the scope ends, the items can still use the data they were made from
            WriteSection(rm2, outputs[0]);
            if (!isDefault)
            {
                ((BaseTwinSection)sm2).ChangeItemPosition(Constants.SCENERY_SECENERY_ITEM, 1);
                WriteSection(sm2, outputs[1]);
            }
        }

        cache.Record($"chunk:{chunk.URI}", accessedAssets.Keys, outputs, factory.LinkedChunks);
        Log.WriteLine($"Finished writing {chunk.Alias}");
    }

    private void ResolveChunkResource(ITwinItemFactory factory, LevelChunk chunk, IAsset asset, ITwinSection rm2, ITwinSection sm2, bool isDefault)
    {
        if (!isDefault && asset is Scenery or ChunkLinks)
        {
            if (asset is Scenery scenery)
            {
                var sceneryData = ((IAsset)scenery).GetData<SceneryData>();
                sceneryData.SkydomeID = chunk.Skydome;

                var collision = AssetManager.GetAsset(sceneryData.Collision);
                collision.ResolveChunkResources(factory, rm2);
            }

            asset.ResolveChunkResources(factory, sm2);
        }
        else if (asset is SoundEffect)
        {
            asset.ResolveChunkResources(factory, rm2.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(asset.Section));
        }
        else
        {
            asset.ResolveChunkResources(factory, rm2);
        }
    }

    private static void WriteSection(ITwinSection section, string path)
    {
        using var file = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
        using var writer = new System.IO.BinaryWriter(file);
        section.Write(writer);
    }

    public void PackAssetsPS2(BuildProfile? profile = null)
    {
        var total = Stopwatch.StartNew();
        if (PackAssets(GamePlatform.PS2, profile))
        {
            CreatePs2ArchivesAndIso();
        }

        Log.WriteLine($"The PS2 build took {total.Elapsed}");
    }

    public void PackAssetsXbox(BuildProfile? profile = null)
    {
        var total = Stopwatch.StartNew();
        if (PackAssets(GamePlatform.Xbox, profile))
        {
            CreateXboxGame();
        }

        Log.WriteLine($"The Xbox build took {total.Elapsed}");
    }

    public void Build(BuildProfile profile)
    {
        if (profile.Platform == GamePlatform.Xbox)
        {
            PackAssetsXbox(profile);
        }
        else
        {
            PackAssetsPS2(profile);
        }
    }

    // Writes the platform's chunks and global files, the files that didn't change since the last build are kept. Every path is
    // absolute: saving a chunk while the build ran changed the current directory, and the global files went somewhere else or nowhere
    private bool PackAssets(GamePlatform platform, BuildProfile? profile = null)
    {
        var globalPackage = GetGlobalPackage(platform);
        if (!globalPackage.Enabled)
        {
            Log.WriteLine($"{globalPackage.Name} package MUST be enabled to compile the project", Log.LogType.Error);
            return false;
        }

        var excludedChunks = profile?.ExcludedChunkUris() ?? [];
        if (profile != null)
        {
            Log.WriteLine($"Building with the {profile.Name} profile, {excludedChunks.Count} chunks left out");
        }

        var factory = CreateFactory(platform);
        factory.ExcludedChunks = excludedChunks;
        var assetManager = AssetManager;
        using var memoryGate = new MemoryGate((long)(Preferences.GetPreference<Double>(Preferences.BuildMemoryBudget) * 1024 * 1024));

        Log.WriteLine("Creating build directories...");
        var filesPath = GetBuildFilesPath(platform);
        System.IO.Directory.CreateDirectory(filesPath);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(ProjectPath, "build", "image"));

        Log.WriteLine("Building archives...");
        foreach (var folder in new[] { "Extras", "Language", "Levels", "Startup" })
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(filesPath, folder));
        }

        UInt32 totalGlobals = 0;
        UInt32 currentGlobalsCount = 0;
        Log.WriteLine("Writing Levels...");
        var phaseTimer = Stopwatch.StartNew();
        var chunksFolder = GetLevelsFolders(platform);

        var cache = BuildCache.Load(ProjectPath, AssetManager);
        cache.ExcludedChunks = excludedChunks;
        var levelsPath = System.IO.Path.Combine(filesPath, "Levels");
        var jobs = new List<(LevelChunk Chunk, string[] Outputs)>();
        var createdDirectories = new List<string>();
        var leftOut = new LeftOutChunks(excludedChunks, platform, filesPath, DiscContentPathPS2);
        var claimedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in chunksFolder)
        {
            CollectChunks(cache, folder, levelsPath, platform, jobs, createdDirectories, leftOut, claimedOutputs);
        }

        try
        {
            WriteChunks(factory, cache, jobs, memoryGate);
        }
        finally
        {
            // The chunks written before one failed are kept for the next build
            cache.Save();
            // Chunk folders only held their chunk's own assets, levels are written next to them
            foreach (var directory in Enumerable.Reverse(createdDirectories).Where(directory => !System.IO.Directory.EnumerateFileSystemEntries(directory).Any()))
            {
                System.IO.Directory.Delete(directory);
            }
        }

        Log.WriteLine($"Finished writing Levels in {phaseTimer.Elapsed}");
        foreach (var globalFolder in new[] { "Extras", "Language" })
        {
            Log.WriteLine($"Writing {globalFolder}...");
            phaseTimer.Restart();
            var folderUri = globalPackage.GetPackageFolder().FindChild<Folder>(globalFolder);
            if (folderUri != LabURI.Empty)
            {
                ResolveGlobalAssets(factory, cache, assetManager.GetAsset<Folder>(folderUri).Children, System.IO.Path.Combine(filesPath, globalFolder), ref totalGlobals,
                    ref currentGlobalsCount);
            }

            Log.WriteLine($"Finished writing {globalFolder} in {phaseTimer.Elapsed}");
        }

        Log.WriteLine("Writing Startup...");
        phaseTimer.Restart();
        // Startup and startup are the same folder on Windows but two separate ones on case sensitive file systems
        var startupFolders = globalPackage.GetPackageFolder().Children.Select(assetManager.GetAsset).OfType<Folder>()
            .Where(folder => folder.Name.Equals("Startup", StringComparison.OrdinalIgnoreCase)).ToList();
        var defaultChunk = startupFolders.Select(folder => folder.FindChild<LevelChunk>("default")).First(uri => uri != LabURI.Empty);
        var startupPath = System.IO.Path.Combine(filesPath, "Startup");
        BuildChunk(factory, cache, assetManager.GetAsset<LevelChunk>(defaultChunk), startupPath, true, platform);
        cache.Save();
        // Startup's other files were always exported right after the default chunk, which leaves the factory resolving for it
        factory.IsDefaultResolution = true;
        var childrenCopy = startupFolders.SelectMany(folder => folder.Children).Where(e => !e.GetUri().EndsWith("/default")).ToList();
        ResolveGlobalAssets(factory, cache, childrenCopy, startupPath, ref totalGlobals, ref currentGlobalsCount);
        cache.Save();

        Log.WriteLine($"Finished writing Startup in {phaseTimer.Elapsed}");
        Log.WriteLine("Finished writing main archive files!");
        return true;
    }

    /// <summary>
    /// Puts the Xbox game together in the build folder: the disc's files with the ones the build wrote in place of theirs
    /// </summary>
    /// <remarks>
    /// The Xbox's file system doesn't tell names apart by case, the build's files replace the disc's ones whatever case their names have.
    /// Files that are already the same as their source are left alone so building again doesn't copy the whole disc
    /// </remarks>
    public void CreateXboxGame()
    {
        if (string.IsNullOrEmpty(DiscContentPathXbox) || !System.IO.Directory.Exists(DiscContentPathXbox))
        {
            Log.WriteLine("The Xbox disc content wasn't found, the game can't be put together", Log.LogType.Error);
            return;
        }

        var phaseTimer = Stopwatch.StartNew();
        var gamePath = GetXboxGamePath();
        var filesPath = GetBuildFilesPath(GamePlatform.Xbox);
        Log.WriteLine("Putting the Xbox game together...");
        var built = System.IO.Directory.Exists(filesPath)
            ? System.IO.Directory.EnumerateFiles(filesPath, "*", System.IO.SearchOption.AllDirectories).ToList()
            : [];
        var builtPaths = built.Select(file => System.IO.Path.GetRelativePath(filesPath, file)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copied = 0;
        foreach (var file in System.IO.Directory.EnumerateFiles(DiscContentPathXbox, "*", System.IO.SearchOption.AllDirectories))
        {
            var relativePath = System.IO.Path.GetRelativePath(DiscContentPathXbox, file);
            if (builtPaths.Contains(relativePath))
            {
                continue;
            }

            copied += SyncFile(file, System.IO.Path.Combine(gamePath, relativePath)) ? 1 : 0;
        }

        var replaced = 0;
        foreach (var file in built)
        {
            replaced += SyncFile(file, ResolveCaseInsensitive(gamePath, System.IO.Path.GetRelativePath(filesPath, file))) ? 1 : 0;
        }

        Log.WriteLine($"Copied {copied} files of the disc and {replaced} built files in {phaseTimer.Elapsed}. The game is in {gamePath}");

        phaseTimer.Restart();
        var imagePath = System.IO.Path.Combine(ProjectPath, "build", "image", $"{Name} (Xbox).iso");
        Log.WriteLine("Creating Xbox ISO image...");
        var reported = 0;
        XboxImageMaker.Make(gamePath, imagePath, progress =>
        {
            // Every tenth of the way is enough to see it moving
            var tenth = (Int32)(progress * 10);
            if (tenth > reported)
            {
                reported = tenth;
                Log.WriteLine($"ISO creating progress {progress * 100:F0}%...");
            }
        });
        Log.WriteLine($"Finished creating the ISO in {phaseTimer.Elapsed}! Check the {System.IO.Path.GetDirectoryName(imagePath)} folder!");
    }

    public string GetXboxGamePath() => System.IO.Path.Combine(ProjectPath, "build", "xbox", "game");

    private static bool SyncFile(string source, string destination)
    {
        var sourceInfo = new System.IO.FileInfo(source);
        var destinationInfo = new System.IO.FileInfo(destination);
        if (destinationInfo.Exists && destinationInfo.Length == sourceInfo.Length && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return false;
        }

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        System.IO.File.Copy(source, destination, true);
        System.IO.File.SetLastWriteTimeUtc(destination, sourceInfo.LastWriteTimeUtc);
        return true;
    }

    // The path under the root with every folder and file that already exists in some case taking that case
    private static string ResolveCaseInsensitive(string root, string relativePath)
    {
        var current = root;
        foreach (var part in relativePath.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
        {
            var existing = System.IO.Directory.Exists(current)
                ? System.IO.Directory.EnumerateFileSystemEntries(current).FirstOrDefault(entry => System.IO.Path.GetFileName(entry).Equals(part, StringComparison.OrdinalIgnoreCase))
                : null;
            current = existing ?? System.IO.Path.Combine(current, part);
        }

        return current;
    }

    public void CreatePs2ArchivesAndIso()
    {
        var phaseTimer = Stopwatch.StartNew();
        Log.WriteLine("Packing into BD/BH archives...");
        {
            var bd = new PS2BD("", $"{DiscContentPathPS2}/Crash6/Crash.BH");
            using var bdFile = new System.IO.FileStream($"{DiscContentPathPS2}/Crash6/Crash.BD",
                System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var bdWriter = new System.IO.BinaryWriter(bdFile);
            bd.BuildRecords($"{ProjectPath}/build/archives");
            bd.Write(bdWriter);
            bdWriter.Flush();
            bdWriter.Close();
        }
        GC.Collect();

        Log.WriteLine($"Finished packing the archives in {phaseTimer.Elapsed}");
        phaseTimer.Restart();
        Log.WriteLine("Creating PS2 ISO image...");
        if (!System.IO.Directory.Exists($"{ProjectPath}/build/image"))
        {
            System.IO.Directory.CreateDirectory($"{ProjectPath}/build/image");
        }
        var progress = Ps2ImageMaker.StartPacking(DiscContentPathPS2!, $"{ProjectPath}/build/image/{Name}.iso");
        Thread.Sleep(TimeSpan.FromSeconds(0.5));
        progress = Ps2ImageMaker.PollProgress();
        while (!progress.Finished)
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            progress = Ps2ImageMaker.PollProgress();
            Log.WriteLine($"ISO creating progress {progress.ProgressPercentage * 100:F2}%...");
        }
        Log.WriteLine($"Finished creating the ISO in {phaseTimer.Elapsed}! Check the {ProjectPath}/build/image folder!");
    }

    // Writes the assets' files into the directory, a folder's into a directory of its own in it
    internal void ResolveGlobalAssets(ITwinItemFactory factory, BuildCache cache, List<LabURI> assets, string directory, ref UInt32 totalGlobals, ref UInt32 currentGlobalsCount)
    {
        var assetManager = AssetManager.Get();
        totalGlobals += (UInt32)assets.Select(assetManager.GetAsset).Count(a => a is not Folder && !a.SkipExport);
        foreach (var item in assets)
        {
            var asset = assetManager.GetAsset(item);
            // Assets embedded in others (a PSM's PTCs) are written by their owner
            if (asset is not Folder && asset.SkipExport)
            {
                continue;
            }

            if (asset is not Folder folder)
            {
                currentGlobalsCount++;
                var output = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, asset.ExportFileName));
                var cacheKey = $"file:{asset.URI}";
                if (cache.IsUpToDate(cacheKey, [output]))
                {
                    Log.WriteLine($"({currentGlobalsCount}/{totalGlobals}) {asset.Name} didn't change since the last build, keeping it", Log.LogType.Debug);
                    continue;
                }

                Log.WriteLine($"Writing ({currentGlobalsCount}/{totalGlobals}) {asset.Name}...");
                var accessedAssets = new System.Collections.Concurrent.ConcurrentDictionary<IAsset, byte>();
                accessedAssets.TryAdd(asset, 0);
                using (assetManager.RecordAccessedAssets(accessedAssets))
                using (new AssetDataScope())
                {
                    try
                    {
                        asset.ExportToFile(factory, directory);
                    }
                    catch (Exception ex) when (ex is not BuildException)
                    {
                        throw new BuildException(asset.Name, ex);
                    }
                }

                cache.Record(cacheKey, accessedAssets.Keys, [output]);
                continue;
            }

            if (asset.Name == "PSM" || asset.Name.Contains("ptc") || asset.Name.Contains("Playstation font") || asset.Name.Contains("SoundEffect"))
            {
                continue;
            }

            var subdirectory = System.IO.Path.Combine(directory, asset.Name);
            System.IO.Directory.CreateDirectory(subdirectory);
            ResolveGlobalAssets(factory, cache, folder.Children, subdirectory, ref totalGlobals, ref currentGlobalsCount);
        }
    }

    // The chunks that changed since the last build with the files they're written to. A level is written next to its chunk's folder
    // The levels folders at the root of the version's enabled packages, the other version's chunks are built for it
    internal List<Folder> GetLevelsFolders(GamePlatform platform)
    {
        return (from dependencyUri in BasePackage.Dependencies
                where GetPlatform(dependencyUri) == platform
                let package = AssetManager.GetAsset<Package>(dependencyUri)
                where package.Enabled
                let folder = package.GetPackageFolder().Children.Where(AssetManager.DoesAssetExist).Select(AssetManager.GetAsset).OfType<Folder>()
                    .FirstOrDefault(child => child.Alias == Folder.LevelsFolderName)
                where folder != null
                select folder).ToList();
    }

    private void CollectChunks(BuildCache cache, Folder currentFolder, string directory, GamePlatform platform, List<(LevelChunk Chunk, string[] Outputs)> jobs,
        List<string> createdDirectories, LeftOutChunks leftOut, ISet<string> claimedOutputs)
    {
        foreach (var item in currentFolder.Children)
        {
            var asset = AssetManager.GetAsset(item);
            if (asset is LevelChunk chunk)
            {
                var outputs = GetChunkOutputs(chunk, System.IO.Path.GetDirectoryName(directory)!, false, platform);
                // The game has one file for a path, two packages' chunks at it would be written over each other
                if (!claimedOutputs.Add(outputs[0]))
                {
                    Log.WriteLine($"{chunk.Alias} of {AssetManager.GetAsset(chunk.Package).Name} is at the same place as a chunk of another package, only the first one is built",
                        Log.LogType.Warning);
                    break;
                }

                if (leftOut.Contains(chunk))
                {
                    if (!leftOut.Keep(chunk, outputs))
                    {
                        jobs.Add((chunk, outputs));
                    }
                }
                else if (cache.IsUpToDate($"chunk:{chunk.URI}", outputs))
                {
                    Log.WriteLine($"{chunk.Alias} didn't change since the last build, keeping it");
                }
                else
                {
                    jobs.Add((chunk, outputs));
                }

                // Only one level chunk file can exist per folder
                break;
            }

            if (asset is Folder innerFolder)
            {
                var innerDirectory = System.IO.Path.Combine(directory, innerFolder.Name);
                System.IO.Directory.CreateDirectory(innerDirectory);
                createdDirectories.Add(innerDirectory);
                CollectChunks(cache, innerFolder, innerDirectory, platform, jobs, createdDirectories, leftOut, claimedOutputs);
            }
        }
    }

    // Chunks a build profile leaves out aren't written. The Xbox game takes the disc's files of the ones the build folder doesn't have,
    // the PS2 archive is made of the build folder alone, so a left out chunk that was never built gets its files from the disc's
    // archive, the last build's or the game's own, and is only written when that doesn't have it either
    internal sealed class LeftOutChunks(IReadOnlySet<LabURI> excluded, GamePlatform platform, string filesPath, string? discContentPath)
    {
        private DiscArchive? _discArchive;
        private bool _discArchiveOpened;

        public bool Contains(LevelChunk chunk) => excluded.Contains(chunk.URI);

        // Whether the chunk's files are taken care of, false when it has to be written
        public bool Keep(LevelChunk chunk, string[] outputs)
        {
            if (platform == GamePlatform.Xbox || outputs.All(System.IO.File.Exists))
            {
                Log.WriteLine($"{chunk.Alias} is left out by the build profile, keeping its files");
                return true;
            }

            if (!_discArchiveOpened)
            {
                _discArchiveOpened = true;
                _discArchive = DiscArchive.Open(discContentPath);
            }

            var files = outputs.Select(output => (System.IO.Path.GetRelativePath(filesPath, output), output)).ToList();
            if (_discArchive != null && _discArchive.TryCopy(files))
            {
                Log.WriteLine($"{chunk.Alias} is left out by the build profile and was never built, taking its files from the disc's archive");
                return true;
            }

            Log.WriteLine($"{chunk.Alias} is left out by the build profile but neither the build nor the disc's archive has it, writing it so the archive has it");
            return false;
        }
    }

    // Chunks build in parallel while the build stays within its memory budget, only writing their files waits on the disk
    private void WriteChunks(ITwinItemFactory factory, BuildCache cache, List<(LevelChunk Chunk, string[] Outputs)> jobs, MemoryGate gate)
    {
        var started = 0;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        // Chunks take them one at a time, the heavy ones would otherwise end up waiting behind each other
        Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(jobs, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), options, job =>
        {
            gate.Enter();
            try
            {
                Log.WriteLine($"Level ({Interlocked.Increment(ref started)}/{jobs.Count}) {job.Chunk.Name}...");
                WriteChunk(factory, cache, job.Chunk, job.Outputs, false);
            }
            finally
            {
                gate.Exit();
            }
        });
    }
}