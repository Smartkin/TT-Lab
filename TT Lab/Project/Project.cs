using Caliburn.Micro;
using Newtonsoft.Json;
using System;
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
using Twinsanity.TwinsanityInterchange.Interfaces;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Project;

/// <summary>
/// Core project class
/// </summary>
public class Project : IProject
{
    private const string CURRENT_VERSION = "0.5.0";

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

    public void Serialize()
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
        // Serialize all the assets
        System.IO.Directory.SetCurrentDirectory("assets");
        var query = from asset in AssetManager.GetAssets()
            group asset by asset.Type;
        var assetTypesQuery = query as IGrouping<Type, IAsset>[] ?? query.ToArray();
        var tasks = new Task[assetTypesQuery.Length];
        var index = 0;
        var startAsset = DateTime.Now;
        foreach (var group in assetTypesQuery)
        {
            tasks[index++] = Task.Factory.StartNew(() =>
            {
                Log.WriteLine($"Serializing {group.Key.Name}...");
                var now = DateTime.Now;
#if !DEBUG
                    try
                    {
#endif
                foreach (var asset in group)
                {
                    if (asset.IsInternal)
                    {
                        continue;
                    }
                    
                    asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
                }
#if !DEBUG
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine($"Error serializing: {ex.Message}");
                    }
#endif
                var span = DateTime.Now - now;
                Log.WriteLine($"Finished serializing {group.Key.Name} in {span}");
            });
        }
        Task.WaitAll(tasks);
        foreach (var task in tasks)
        {
            task.Dispose();
        }

        Log.WriteLine($"Serialized assets in {(DateTime.Now - startAsset)}");
        
        var startUnload = DateTime.Now;
        Log.WriteLine($"Unloading all the loaded data...");
        foreach (var asset in AssetManager.GetAssets())
        {
            if (asset.IsInternal)
            {
                continue;
            }
            
            asset.Serialize(SerializationFlags.FixReferences);
        }
        Log.WriteLine($"Finished unloading the data in {(DateTime.Now - startUnload)}");
        
        Log.WriteLine("Deleting empty folders...");
        System.IO.Directory.SetCurrentDirectory(path);
        System.IO.Directory.SetCurrentDirectory("assets");
        var dirInfo = new System.IO.DirectoryInfo($"{path}/assets");
        DeleteEmptyFolders(dirInfo);
        Log.WriteLine("Finished deleting empty folders...");
        
        System.IO.Directory.SetCurrentDirectory(path);
    }

    private void DeleteEmptyFolders(System.IO.DirectoryInfo root)
    {
        var dirsToDelete = new List<System.IO.DirectoryInfo>();
        foreach (var dir in root.GetDirectories())
        {
            if (!dir.GetFileSystemInfos().Any())
            {
                dirsToDelete.Add(dir);
            }

            foreach (var childDir in dir.GetDirectories())
            {
                DeleteEmptyFolders(childDir);
            }
        }

        foreach (var dirToDelete in dirsToDelete)
        {
            dirToDelete.Delete();
        }
    }

    public static void Deserialize(string projectPath)
    {
        Project? pr;
        using (System.IO.FileStream fs = new(projectPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
        using (System.IO.BinaryReader reader = new(fs))
        {
            var prText = new string(reader.ReadChars((Int32)fs.Length));
            pr = JsonConvert.DeserializeObject<Project>(prText);
        }
        if (pr == null)
        {
            throw new ProjectException("Failed to deserialize the project!");
        }
        if (pr.Version != CURRENT_VERSION)
        {
            throw new ProjectException("The provided version of the project is not supported!");
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
                assets.Add(asset.Key, asset.Value);
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

    public void UnpackAssetsPS2()
    {
        if (string.IsNullOrEmpty(DiscContentPathPS2))
        {
            Log.WriteLine("No PS2 assets provided, skipped...");
            return;
        }
        
        ResolverManager.Start();

        Dictionary<LabURI, IAsset> assets = new();

        var archivePaths = System.IO.Directory.GetFiles(System.IO.Path.Combine(DiscContentPathPS2, "Crash6"), "*.BD", System.IO.SearchOption.TopDirectoryOnly);
        var archive = new PS2BD(archivePaths[0].Replace(".BD", ".BH"), "");
        Log.WriteLine("Reading game archives...");
        using (System.IO.FileStream fs = new(archivePaths[0], System.IO.FileMode.Open, System.IO.FileAccess.Read))
        using (System.IO.BinaryReader reader = new(fs))
        {
            archive.Read(reader, (int)fs.Length);
        }
        
        // Maps graph ID to behaviour starter
        var starterMap = new Dictionary<string, TwinBehaviourStarter>();
        Log.WriteLine("Creating behaviour starter map...");
        foreach (var item in archive.Items)
        {
            var pathLow = item.Header.Path.ToLower();
            var isRm2 = pathLow.EndsWith(".rm2");
            var isDefault = pathLow.EndsWith("default.rm2");
            if (!isRm2)
            {
                continue;
            }
                
            ITwinSection? chunk = null;
            if (isDefault)
            {
                chunk = new PS2Default();
            }
            else if (isRm2)
            {
                chunk = new PS2AnyTwinsanityRM2();
            }
            using System.IO.MemoryStream ms = new(item.Data);
            using System.IO.BinaryReader reader = new(ms);

            // Fill chunk data
            chunk!.Read(reader, (Int32)ms.Length);
            
            Log.WriteLine($"Collecting {System.IO.Path.GetFileName(pathLow[..^4])} behaviour starters...");
            
            var code = chunk.GetItem<PS2AnyCodeSection>(Constants.LEVEL_CODE_SECTION);
            var items = code.GetItem<PS2AnyBehavioursSection>(Constants.CODE_BEHAVIOURS_SECTION);
                
            for (var i = 0; i < items.GetItemsAmount(); ++i)
            {
                var asset = items.GetItem<TwinBehaviourWrapper>(items.GetItem(i).GetID());
                var isStarter = asset.GetID() % 2 == 0;
                if (!isStarter)
                {
                    continue;
                }

                var starter = (TwinBehaviourStarter)asset;
                var starterStr = (starter.Assigners[0].Behaviour - 1).ToString();
                if (starterMap.ContainsKey(starterStr))
                {
                    starterStr += pathLow;
                }
                starterMap.Add(starterStr, (TwinBehaviourStarter)asset);
            }
        }

        var gameObjectResolver = new GameObjectResolver(starterMap);
        var behaviourResolver = new BehaviourResolver(starterMap);
        var behaviourSequenceResolver = new BehaviourSequenceResolver();
        var skydomeResolver = new SkydomeResolver();
        
        var chunkResolvers = new List<IAssetResolver>();

        // Unpack all assets from chunks
        foreach (var item in archive.Items)
        {
            var path = item.Header.Path.Replace('\\', System.IO.Path.DirectorySeparatorChar);
            var pathLow = item.Header.Path.Replace('\\', System.IO.Path.DirectorySeparatorChar).ToLower();
            var isRm2 = pathLow.EndsWith(".rm2");
            var isSm2 = pathLow.EndsWith(".sm2");
            var isDefault = pathLow.EndsWith("default.rm2");
            var isTxt = pathLow.EndsWith(".txt");
            var isFrontend = pathLow.EndsWith("frontend.bin");
            var isPsm = pathLow.EndsWith(".psm");
            var isFont = pathLow.EndsWith(".psf");
            var isPtc = pathLow.EndsWith(".ptc");
            var isIco = pathLow.EndsWith(".ico");
            Log.WriteLine($"Unpacking {System.IO.Path.GetFileName(pathLow)}...");
            using System.IO.MemoryStream ms = new(item.Data);

            if (isTxt || isFont || isPsm || isPtc || isFrontend || isIco)
            {
                var resourceName = System.IO.Path.GetFileName(path)[..^4];
                path = path[..^4];
                var otherFolders = path.Split(System.IO.Path.DirectorySeparatorChar);
                var resourcePath = string.Join(System.IO.Path.DirectorySeparatorChar, otherFolders[..^1]);

                // Check for text files
                if (isTxt)
                {
                    using System.IO.BinaryReader textReader = new(ms, Encoding.Latin1);
                    var text = textReader.ReadChars((int)ms.Length);
                    var textFile = new TextFile(GlobalPackagePS2.URI, true, pathLow, resourceName, new String(text))
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
                    var font = new PS2PSF();
                    font.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var fontAsset = new Font(GlobalPackagePS2.URI, true, pathLow, resourceName, font)
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
                    var psm = new PS2PSM();
                    psm.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var psmAsset = new PSM(GlobalPackagePS2.URI, true, pathLow, resourceName, psm)
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
                    var ptc = new PS2PTC();
                    ptc.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var ptcAsset = new PTC(GlobalPackagePS2.URI, true, pathLow, resourceName, ptc)
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
                    var ico = new SaveIcon(GlobalPackagePS2.URI, false, "", resourceName, item.Data)
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
                    var frontend = new PS2Frontend();
                    frontend.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                    var uiLibrary = new UiSoundLibrary(GlobalPackagePS2.URI, false, "", "Frontend", frontend)
                    {
                        Alias = "UI Sound Library",
                        GlobalPath = resourcePath
                    };
                    uiLibrary.RegenerateLinks();
                    assets.Add(uiLibrary.URI, uiLibrary);
                    continue;
                }
            }

            using System.IO.BinaryReader reader = new(ms);

            // Check for chunk file
            if (!isRm2 && !isSm2)
            {
                continue;
            }
            
            ITwinSection? chunk = null;
            IAssetResolver? assetResolver = null;
            if (isDefault)
            {
                chunk = new PS2Default();
                assetResolver = new DefaultChunkResolver(gameObjectResolver, behaviourResolver, behaviourSequenceResolver);
            }
            else if (isRm2)
            {
                chunk = new PS2AnyTwinsanityRM2();
                assetResolver = new ResourceChunkResolver(gameObjectResolver, behaviourResolver, behaviourSequenceResolver);
            }
            else if (isSm2)
            {
                chunk = new PS2AnyTwinsanitySM2();
                assetResolver = new SceneryChunkResolver(skydomeResolver);
            }

            // Fill chunk data
            chunk!.Read(reader, (Int32)ms.Length);
            
            // assetResolver!.CreateAssetsFromChunk(chunk, isDefault ? GlobalPackagePS2 : Ps2Package);
            ResolverManager.PerformResolve(isDefault ? GlobalPackagePS2 : Ps2Package, pathLow[..^4], chunk, assetResolver!);

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

    public void UnpackAssetsXbox()
    {
        if (string.IsNullOrEmpty(DiscContentPathXbox))
        {
            Log.WriteLine("No XBox assets provided, skipped...");
            return;
        }
            
        throw new NotImplementedException();
    }

    public void PackChunk(LabURI chunkUri, ITwinItemFactory? itemFactory = null)
    {
        var factory = itemFactory ?? new PS2ItemFactory();
        var assetManager = AssetManager.Get();
        var chunk = assetManager.GetAsset<LevelChunk>(chunkUri);
        factory.ChunkPath = chunk.AdditionalPath!;
        
        System.IO.Directory.SetCurrentDirectory(ProjectPath);
        System.IO.Directory.CreateDirectory("build");
        System.IO.Directory.SetCurrentDirectory("build");
        System.IO.Directory.CreateDirectory("archives");
        System.IO.Directory.SetCurrentDirectory("archives");
        if (chunk.Name == "default")
        {
            Log.WriteLine("Writing Default chunk...");
            System.IO.Directory.CreateDirectory("Startup");
            System.IO.Directory.SetCurrentDirectory("Startup");
            var @default = factory.GenerateDefault();
            
            foreach (var asset in chunk.ChunkResources.Select(child => assetManager.GetAsset(child)))
            {
                Log.WriteLine($"Writing {asset.Name}...");
                asset.ResolveChunkResources(factory, @default);
            }
            
            ((BaseTwinSection)@default).ChangeItemPosition(Constants.LEVEL_COLLISION_ITEM, 2);
            ((BaseTwinSection)@default).ChangeItemPosition(Constants.LEVEL_PARTICLES_ITEM, 2);
            
            using var defaultFile = new System.IO.FileStream($"Default.rm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var defaultWriter = new System.IO.BinaryWriter(defaultFile);
            @default.Write(defaultWriter);
            defaultWriter.Flush();
            defaultWriter.Close();
            Log.WriteLine("Finished writing Default chunk!");
            return;
        }
        System.IO.Directory.CreateDirectory("Levels");
        System.IO.Directory.SetCurrentDirectory("Levels");
        var chunkLevelPath = chunk.AdditionalPath!;
        foreach (var pathToken in chunkLevelPath.Split(System.IO.Path.DirectorySeparatorChar).Skip(1).SkipLast(1))
        {
            System.IO.Directory.CreateDirectory(pathToken.Replace(System.IO.Path.DirectorySeparatorChar.ToString(), ""));
            System.IO.Directory.SetCurrentDirectory(pathToken.Replace(System.IO.Path.DirectorySeparatorChar.ToString(), ""));
        }
        Log.WriteLine($"Writing Level {chunk.Alias}...");
        var rm2 = factory.GenerateRM();
        var sm2 = factory.GenerateSM();

        foreach (var asset in chunk.ChunkResources.Select(child => assetManager.GetAsset(child)))
        {
            Log.WriteLine($"Writing {asset.Name}...");
            if (asset is Scenery or ChunkLinks)
            {
                if (asset is Scenery scenery)
                {
                    var sceneryData = ((IAsset)scenery).GetData<SceneryData>();
                    sceneryData.SkydomeID = chunk.Skydome;
                    
                    var collision = assetManager.GetAsset(sceneryData.Collision);
                    collision.ResolveChunkResources(factory, rm2);
                }
                
                asset.ResolveChunkResources(factory, sm2);
            }
            else
            {
                asset.ResolveChunkResources(factory, rm2);
            }
        }

        ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_COLLISION_ITEM, 2);
        ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_PARTICLES_ITEM, 2);

        ((BaseTwinSection)sm2).ChangeItemPosition(Constants.SCENERY_SECENERY_ITEM, 1);

        using var rm2File = new System.IO.FileStream($"{chunk.Name}.rm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
        using var rm2Writer = new System.IO.BinaryWriter(rm2File);
        rm2.Write(rm2Writer);
        rm2Writer.Flush();
        rm2Writer.Close();

        using var sm2File = new System.IO.FileStream($"{chunk.Name}.sm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
        using var sm2Writer = new System.IO.BinaryWriter(sm2File);
        sm2.Write(sm2Writer);
        sm2Writer.Flush();
        sm2Writer.Close();
            
        Log.WriteLine($"Finished writing {chunk.Alias}");
    }

    public void PackAssetsPS2()
    {
        System.IO.Directory.SetCurrentDirectory(ProjectPath);

        if (!GlobalPackagePS2.Enabled)
        {
            Log.WriteLine("Global PS2 package MUST be enabled to compile the project", Log.LogType.Error);
            return;
        }

        var factory = new PS2ItemFactory();
        var assetManager = AssetManager;

        Log.WriteLine("Creating build directories...");
        System.IO.Directory.CreateDirectory("build");
        System.IO.Directory.SetCurrentDirectory("build");
        System.IO.Directory.CreateDirectory("archives");
        System.IO.Directory.CreateDirectory("image");

        Log.WriteLine("Building archives...");
        System.IO.Directory.SetCurrentDirectory("archives");
        System.IO.Directory.CreateDirectory("Extras");
        System.IO.Directory.CreateDirectory("Language");
        System.IO.Directory.CreateDirectory("Levels");
        System.IO.Directory.CreateDirectory("Startup");

        System.IO.Directory.SetCurrentDirectory("Levels");
        // Log.WriteLine("Writing Levels...");
        // var chunksFolder = (from dependencyUri in BasePackage.Dependencies
        //     let dependency = assetManager.GetAsset<Package>(dependencyUri)
        //     where dependency.Enabled
        //     let packageFolder = dependency.GetPackageFolder()
        //     let folder = packageFolder.FindChild("levels")
        //     where folder != LabURI.Empty
        //     select assetManager.GetAsset<Folder>(folder)).ToList();
        UInt32 totalGlobals = 0;
        UInt32 currentGlobalsCount = 0;
        // foreach (var folder in chunksFolder)
        // {
        //     ResolveAndWriteChunks(factory, folder, ref totalGlobals, ref currentGlobalsCount);
        // }
        //
        // Log.WriteLine("Writing Extras...");
        // System.IO.Directory.SetCurrentDirectory("../Extras");
        //
        // var extrasFolder = assetManager.GetAsset<Folder>(GlobalPackagePS2.GetPackageFolder().FindChild<Folder>("Extras"));
        // ResolveGlobalAssets(factory, extrasFolder.Children, ref totalGlobals, ref currentGlobalsCount);
        //
        // System.IO.Directory.SetCurrentDirectory("../Language");
        // Log.WriteLine("Writing Language...");
        // var languageFolder = assetManager.GetAsset<Folder>(GlobalPackagePS2.GetPackageFolder().FindChild<Folder>("Language"));
        // ResolveGlobalAssets(factory, languageFolder.Children, ref totalGlobals, ref currentGlobalsCount);

        System.IO.Directory.SetCurrentDirectory("../Startup");
        Log.WriteLine("Writing Startup...");
        ResolveAndWriteChunks(factory, new Folder("temp") { Children = [GlobalPackagePS2.GetPackageFolder().FindAndGetChild<Folder>("startup")
            .FindAndGetChild<Folder>("default").FindChild<LevelChunk>("default")] }, ref totalGlobals, ref currentGlobalsCount, true);
        
        var startupFolder = assetManager.GetAsset<Folder>(GlobalPackagePS2.GetPackageFolder().FindChild<Folder>("Startup"));
        ResolveGlobalAssets(factory, startupFolder.Children, ref totalGlobals, ref currentGlobalsCount);

        System.IO.Directory.SetCurrentDirectory("../..");
        Log.WriteLine("Finished writing main archive files!");
        CreatePs2ArchivesAndIso();
    }

    public void CreatePs2ArchivesAndIso()
    {
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

        Log.WriteLine("Creating PS2 ISO image...");
        if (!System.IO.Directory.Exists($"{ProjectPath}/build/image"))
        {
            System.IO.Directory.CreateDirectory($"{ProjectPath}/build/image");
        }
        var progress = Ps2ImageMaker.StartPacking(DiscContentPathPS2!, $"{ProjectPath}/build/image/{Name}.iso");
        while (!progress.Finished)
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            progress = Ps2ImageMaker.PollProgress();
            Log.WriteLine($"ISO creating progress {progress.ProgressPercentage * 100:F2}%...");
        }
        Log.WriteLine($"Finished creating the ISO! Check the {ProjectPath}/build/image folder!");
    }

    public void PackAssetsXbox()
    {
        Log.WriteLine("Packing XBox assets is not supported yet :(");
    }

    private void ResolveGlobalAssets(ITwinItemFactory factory, List<LabURI> assets, ref UInt32 totalGlobals, ref UInt32 currentGlobalsCount)
    {
        var assetManager = AssetManager.Get();
        totalGlobals += (UInt32)assets.Select(assetManager.GetAsset).Count(a => a is not Folder);
        foreach (var item in assets)
        {
            var asset = assetManager.GetAsset(item);
            if (asset is not Folder folder)
            {
                currentGlobalsCount++;
                Log.WriteLine($"Writing ({currentGlobalsCount}/{totalGlobals}) {asset.Name}...");
                asset.ExportToFile(factory);
                continue;
            }

            if (asset.Name == "PSM" || asset.Name.Contains("ptc") || asset.Name.Contains("Playstation font") || asset.Name.Contains("SoundEffect"))
            {
                continue;
            }

            System.IO.Directory.CreateDirectory(asset.Name);
            System.IO.Directory.SetCurrentDirectory(asset.Name);
            ResolveGlobalAssets(factory, folder.Children, ref totalGlobals, ref currentGlobalsCount);
            System.IO.Directory.SetCurrentDirectory("..");
        }
    }

    private void ResolveAndWriteChunks(ITwinItemFactory factory, Folder currentFolder, ref UInt32 scenesTotal, ref UInt32 currentSceneCount, bool isDefault = false)
    {
        var assetManager = AssetManager.Get();
        scenesTotal += (UInt32)currentFolder.Children.Select(assetManager.GetAsset).Count(a => a is LevelChunk);
        foreach (var item in currentFolder.Children)
        {
            var folder = assetManager.GetAsset(item);
            if (folder is LevelChunk chunk)
            {
                currentSceneCount++;
                factory.ChunkPath = chunk.AdditionalPath!;
                Log.WriteLine($"Writing level ({currentSceneCount}/{scenesTotal}) {chunk.Name}...");
                var rm2 = isDefault ? factory.GenerateDefault() : factory.GenerateRM();
                var sm2 = factory.GenerateSM();

                foreach (var asset in chunk.ChunkResources.Select(child => assetManager.GetAsset(child)))
                {
                    Log.WriteLine($"Writing {asset.Name}...");
                    if (!isDefault && asset is Scenery or ChunkLinks)
                    {
                        if (asset is Scenery scenery)
                        {
                            var sceneryData = ((IAsset)scenery).GetData<SceneryData>();
                            sceneryData.SkydomeID = chunk.Skydome;
                            
                            var collision = assetManager.GetAsset(sceneryData.Collision);
                            collision.ResolveChunkResources(factory, rm2);
                        }
                        
                        asset.ResolveChunkResources(factory, sm2);
                    }
                    else
                    {
                        asset.ResolveChunkResources(factory, rm2);
                    }
                }
                
                ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_COLLISION_ITEM, 2);
                ((BaseTwinSection)rm2).ChangeItemPosition(Constants.LEVEL_PARTICLES_ITEM, 2);

                if (!isDefault)
                {
                    ((BaseTwinSection)sm2).ChangeItemPosition(Constants.SCENERY_SECENERY_ITEM, 1);
                    
                    using var rm2File = new System.IO.FileStream($"..{System.IO.Path.DirectorySeparatorChar}{chunk.Name}.rm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    using var rm2Writer = new System.IO.BinaryWriter(rm2File);
                    rm2.Write(rm2Writer);
                    rm2Writer.Flush();
                    rm2Writer.Close();
                    
                    using var sm2File = new System.IO.FileStream(
                        $"..{System.IO.Path.DirectorySeparatorChar}{chunk.Name}.sm2", System.IO.FileMode.Create,
                        System.IO.FileAccess.Write);
                    using var sm2Writer = new System.IO.BinaryWriter(sm2File);
                    sm2.Write(sm2Writer);
                    sm2Writer.Flush();
                    sm2Writer.Close();
                }
                else
                {
                    using var rm2File = new System.IO.FileStream(StringExtensions.CapitalizeFirstChar($"{chunk.Name}.rm2"), System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    using var rm2Writer = new System.IO.BinaryWriter(rm2File);
                    rm2.Write(rm2Writer);
                    rm2Writer.Flush();
                    rm2Writer.Close();
                }

                // Only one level chunk file can exist per folder
                break;
            }
            
            if (folder is Folder innerFolder)
            {
                System.IO.Directory.CreateDirectory(folder.Name);
                System.IO.Directory.SetCurrentDirectory(folder.Name);
                ResolveAndWriteChunks(factory, innerFolder, ref scenesTotal, ref currentSceneCount);
                System.IO.Directory.SetCurrentDirectory("..");
                
                if (!System.IO.Directory.EnumerateFileSystemEntries(folder.Name).Any())
                {
                    System.IO.Directory.Delete(folder.Name);
                }
            }
        }
    }
}