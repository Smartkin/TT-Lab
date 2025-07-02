using Caliburn.Micro;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
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

namespace TT_Lab.Project
{
    /// <summary>
    /// Core project class
    /// </summary>
    public class Project : IProject
    {
        private const string CURRENT_VERSION = "0.2.0";

        public AssetManager AssetManager { get; private set; }

        public Folder Packages { get; private set; }

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
            var tasks = new Task[query.Count() - 2];
            var index = 0;
            DateTime startAsset = DateTime.Now;
            foreach (var group in query)
            {
                if (group.Key.Name is nameof(BlendSkin) or nameof(Skin))
                    continue;
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
                        asset.Serialize(SerializationFlags.SaveData);
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

            // Skins and blend skins are serialized without multithreading because of accessing and changing current directory
            // and needing all the materials and textures serialized
            foreach (var group in query)
            {
                if (group.Key.Name != typeof(BlendSkin).Name && group.Key.Name != typeof(Skin).Name)
                    continue;
                Log.WriteLine($"Serializing {group.Key.Name}...");
                var now = DateTime.Now;
#if !DEBUG
                try
                {
#endif
                foreach (var asset in group)
                {
                    asset.Serialize(SerializationFlags.SaveData);
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
            }
            Log.WriteLine($"Serialized assets in {(DateTime.Now - startAsset)}");
            System.IO.Directory.SetCurrentDirectory(path);
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
            IoC.Get<ProjectManager>().OpenedProject = pr;

            var taskList = new List<Task<Dictionary<LabURI, IAsset>>>();
            // Deserialize assets
            foreach (var dir in System.IO.Directory.GetDirectories("assets"))
            {
                Log.WriteLine($"Opening {dir}...");
                var assetFiles = System.IO.Directory.GetFiles(dir, "*.json", System.IO.SearchOption.AllDirectories);
                taskList.Add(AssetDeserializerFactory.GetAssets(assetFiles));
            }
            Task.WaitAll(taskList.ToArray());
            foreach (var task in taskList.ToArray())
            {
                task.Dispose();
            }
            Log.WriteLine("Finished opening assets...");
            Dictionary<LabURI, IAsset> assets = new();
            pr.AssetManager = new();
            foreach (var assetsList in taskList)
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
            pr.GlobalPackagePS2 = (Package)assets.Values.First(a => a.Name == "Global PS2");
            pr.GlobalPackageXbox = (Package)assets.Values.First(a => a.Name == "Global XBOX");
            pr.Ps2Package = (Package)assets.Values.First(a => a.Name == "PS2");
            pr.XboxPackage = (Package)assets.Values.First(a => a.Name == "XBOX");
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
                    if (System.IO.Directory.Exists(dirPath.Replace(DiscContentPathPS2 + System.IO.Path.DirectorySeparatorChar, "")))
                    {
                        continue;
                    }
                    
                    System.IO.Directory.CreateDirectory(dirPath.Replace(DiscContentPathPS2 + System.IO.Path.DirectorySeparatorChar, ""));
                }

                foreach (var newPath in System.IO.Directory.GetFiles(DiscContentPathPS2, "*.*", System.IO.SearchOption.AllDirectories))
                {
                    System.IO.File.Copy(newPath, newPath.Replace(DiscContentPathPS2 + System.IO.Path.DirectorySeparatorChar, ""), true);
                }

                DiscContentPathPS2 = $"{ProjectPath}\\disc\\ps2";
                
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
                
                DiscContentPathXbox = $"{ProjectPath}\\disc\\xbox";
                
                System.IO.Directory.SetCurrentDirectory("../");
            }
            
            System.IO.Directory.SetCurrentDirectory("../");
        }

        public void CreateBasePackages()
        {
            BasePackage = new Package(Name)
            {
                Mark = FolderMark.Locked
            };
            AssetManager.AddAsset(BasePackage);
            Packages = new Folder(BasePackage.URI, "Packages", null, BasePackage)
            {
                Mark = FolderMark.Locked
            };
            AssetManager.AddAsset(Packages);
            GlobalPackagePS2 = new Package("Global PS2", Packages)
            {
                Variant = "",
                Mark = FolderMark.Locked
            };
            GlobalPackageXbox = new Package("Global XBOX", Packages)
            {
                Variant = "",
                Mark = FolderMark.Locked,
                Enabled = false
            };
            Ps2Package = new Package("PS2", Packages)
            {
                Mark = FolderMark.Locked
            };
            Ps2Package.AddDependency(GlobalPackagePS2.URI);
            XboxPackage = new Package("XBOX", Packages)
            {
                Mark = FolderMark.Locked,
                Enabled = false
            };
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

            Dictionary<LabURI, IAsset> assets = new();

            // Create base package folder for user created assets
            var basePackage = BasePackage;
            var assetsFolder = Folder.CreatePackageFolder(basePackage, "Assets", basePackage.Name);
            assetsFolder.Mark = FolderMark.Locked;
            var blendSkins = Folder.CreatePackageFolder(basePackage, "Blend Skins", assetsFolder, basePackage.Name);
            blendSkins.Mark = FolderMark.Locked;
            var skins = Folder.CreatePackageFolder(basePackage, "Skins", assetsFolder, basePackage.Name);
            skins.Mark = FolderMark.Locked;
            var models = Folder.CreatePackageFolder(basePackage, "Models", assetsFolder, basePackage.Name);
            models.Mark = FolderMark.Locked;
            var rigidModels = Folder.CreatePackageFolder(basePackage, "Rigid models", assetsFolder, basePackage.Name);
            rigidModels.Mark = FolderMark.Locked;
            var meshes = Folder.CreatePackageFolder(basePackage, "Meshes", assetsFolder, basePackage.Name);
            meshes.Mark = FolderMark.Locked;
            var materials = Folder.CreatePackageFolder(basePackage, "Materials", assetsFolder, basePackage.Name);
            materials.Mark = FolderMark.Locked;
            var lods = Folder.CreatePackageFolder(basePackage, "LODs", assetsFolder, basePackage.Name);
            lods.Mark = FolderMark.Locked;
            var skydomes = Folder.CreatePackageFolder(basePackage, "Skydomes", assetsFolder, basePackage.Name);
            skydomes.Mark = FolderMark.Locked;
            var textures = Folder.CreatePackageFolder(basePackage, "Textures", assetsFolder, basePackage.Name);
            textures.Mark = FolderMark.Locked;
            var animations = Folder.CreatePackageFolder(basePackage, "Animations", assetsFolder, basePackage.Name);
            animations.Mark = FolderMark.Locked;
            var codeModels = Folder.CreatePackageFolder(basePackage, "Code models", assetsFolder, basePackage.Name);
            codeModels.Mark = FolderMark.Locked;
            var gameObjects = Folder.CreatePackageFolder(basePackage, "Game objects", assetsFolder, basePackage.Name);
            gameObjects.Mark = FolderMark.Locked;
            var scripts = Folder.CreatePackageFolder(basePackage, "Scripts", assetsFolder, basePackage.Name);
            scripts.Mark = FolderMark.Locked;
            var ogis = Folder.CreatePackageFolder(basePackage, "OGIs/Skeletons", assetsFolder, basePackage.Name);
            ogis.Mark = FolderMark.Locked;
            var sfx = Folder.CreatePackageFolder(basePackage, "Sound Effects", assetsFolder, basePackage.Name);
            sfx.Mark = FolderMark.Locked;
            var en = Folder.CreatePackageFolder(basePackage, "English Sound Effects", assetsFolder, basePackage.Name);
            en.Mark = FolderMark.Locked;
            var fr = Folder.CreatePackageFolder(basePackage, "French Sound Effects", assetsFolder, basePackage.Name);
            fr.Mark = FolderMark.Locked;
            var gr = Folder.CreatePackageFolder(basePackage, "German Sound Effects", assetsFolder, basePackage.Name);
            gr.Mark = FolderMark.Locked;
            var ita = Folder.CreatePackageFolder(basePackage, "Italian Sound Effects", assetsFolder, basePackage.Name);
            ita.Mark = FolderMark.Locked;
            var jpn = Folder.CreatePackageFolder(basePackage, "Japanese Sound Effects", assetsFolder, basePackage.Name);
            jpn.Mark = FolderMark.Locked;
            var spa = Folder.CreatePackageFolder(basePackage, "Spanish Sound Effects", assetsFolder, basePackage.Name);
            spa.Mark = FolderMark.Locked;
            var chunks = Folder.CreatePackageFolder(basePackage, "Chunks", assetsFolder, basePackage.Name);
            chunks.Mark = FolderMark.Locked | FolderMark.ChunksOnly;

            assets.Add(assetsFolder.URI, assetsFolder);
            assets.Add(blendSkins.URI, blendSkins);
            assets.Add(skins.URI, skins);
            assets.Add(models.URI, models);
            assets.Add(rigidModels.URI, rigidModels);
            assets.Add(meshes.URI, meshes);
            assets.Add(materials.URI, materials);
            assets.Add(lods.URI, lods);
            assets.Add(skydomes.URI, skydomes);
            assets.Add(textures.URI, textures);
            assets.Add(animations.URI, animations);
            assets.Add(codeModels.URI, codeModels);
            assets.Add(gameObjects.URI, gameObjects);
            assets.Add(scripts.URI, scripts);
            assets.Add(ogis.URI, ogis);
            assets.Add(sfx.URI, sfx);
            assets.Add(en.URI, en);
            assets.Add(fr.URI, fr);
            assets.Add(gr.URI, gr);
            assets.Add(ita.URI, ita);
            assets.Add(jpn.URI, jpn);
            assets.Add(spa.URI, spa);
            assets.Add(chunks.URI, chunks);


            // Folders hierarchy
            Dictionary<LabURI, Folder> folders = new();

            /// Collections when checking for uniqueness of a resource
            // Graphics
            var graphicsCheck = new Dictionary<uint, Dictionary<String, uint>>
            {
                { Constants.GRAPHICS_BLEND_SKINS_SECTION, new() },
                { Constants.GRAPHICS_LODS_SECTION, new() },
                { Constants.GRAPHICS_MATERIALS_SECTION, new() },
                { Constants.GRAPHICS_MESHES_SECTION, new() },
                { Constants.GRAPHICS_MODELS_SECTION, new() },
                { Constants.GRAPHICS_RIGID_MODELS_SECTION, new() },
                { Constants.GRAPHICS_SKINS_SECTION, new() },
                { Constants.GRAPHICS_SKYDOMES_SECTION, new() },
                { Constants.GRAPHICS_TEXTURES_SECTION, new() }
            };
            // Code
            var codeCheck = new Dictionary<uint, Dictionary<String, uint>>
            {
                { Constants.CODE_ANIMATIONS_SECTION, new() },
                { Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION, new() },
                { Constants.CODE_GAME_OBJECTS_SECTION, new() },
                { Constants.CODE_LANG_ENG_SECTION, new() },
                { Constants.CODE_LANG_FRE_SECTION, new() },
                { Constants.CODE_LANG_GER_SECTION, new() },
                { Constants.CODE_LANG_ITA_SECTION, new() },
                { Constants.CODE_LANG_JPN_SECTION, new() },
                { Constants.CODE_LANG_SPA_SECTION, new() },
                { Constants.CODE_OGIS_SECTION, new() },
                { Constants.CODE_BEHAVIOURS_SECTION, new() },
                { Constants.CODE_SOUND_EFFECTS_SECTION, new() }
            };
            string[] archivePaths = System.IO.Directory.GetFiles(System.IO.Path.Combine(DiscContentPathPS2, "Crash6"), "*.BD", System.IO.SearchOption.TopDirectoryOnly);
            PS2BD archive = new PS2BD(archivePaths[0].Replace(".BD", ".BH"), "");
            Log.WriteLine("Reading game archives...");
            using (System.IO.FileStream fs = new(archivePaths[0], System.IO.FileMode.Open, System.IO.FileAccess.Read))
            using (System.IO.BinaryReader reader = new(fs))
            {
                archive.Read(reader, (int)fs.Length);
            }

            // Create Global PS2 package folders for Default.rm2 assets since they can be referenced from anywhere in the project
            var globalAssets = Folder.CreatePackageFolder(GlobalPackagePS2, "Global Assets", GlobalPackagePS2.Name);
            globalAssets.Mark = FolderMark.Locked;
            var globalBlendSkinsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Blend Skins", globalAssets, GlobalPackagePS2.Name);
            globalBlendSkinsFolder.Mark = FolderMark.Locked;
            var globalSkinsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Skins", globalAssets, GlobalPackagePS2.Name);
            globalSkinsFolder.Mark = FolderMark.Locked;
            var globalModelsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Models", globalAssets, GlobalPackagePS2.Name);
            globalModelsFolder.Mark = FolderMark.Locked;
            var globalRigidModelsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Rigid models", globalAssets, GlobalPackagePS2.Name);
            globalRigidModelsFolder.Mark = FolderMark.Locked;
            var globalMaterialsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Materials", globalAssets, GlobalPackagePS2.Name);
            globalMaterialsFolder.Mark = FolderMark.Locked;
            var globalTexturesFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Textures", globalAssets, GlobalPackagePS2.Name);
            globalTexturesFolder.Mark = FolderMark.Locked;
            var globalMeshesFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Meshes", globalAssets, GlobalPackagePS2.Name);
            globalMeshesFolder.Mark = FolderMark.Locked;
            var globalAnimationsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Animations", globalAssets, GlobalPackagePS2.Name);
            globalAnimationsFolder.Mark = FolderMark.Locked;
            var globalCodeModelsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Code models", globalAssets, GlobalPackagePS2.Name);
            globalCodeModelsFolder.Mark = FolderMark.Locked;
            var globalGameObjectsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Game objects", globalAssets, GlobalPackagePS2.Name);
            globalGameObjectsFolder.Mark = FolderMark.Locked;
            var globalScriptsFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Scripts", globalAssets, GlobalPackagePS2.Name);
            globalScriptsFolder.Mark = FolderMark.Locked;
            var globalOgisFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "OGIs/Skeletons", globalAssets, GlobalPackagePS2.Name);
            globalOgisFolder.Mark = FolderMark.Locked;
            var globalSfxFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalSfxFolder.Mark = FolderMark.Locked;
            var globalEnFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "English Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalEnFolder.Mark = FolderMark.Locked;
            var globalFrFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "French Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalFrFolder.Mark = FolderMark.Locked;
            var globalGrFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "German Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalGrFolder.Mark = FolderMark.Locked;
            var globalItaFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Italian Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalItaFolder.Mark = FolderMark.Locked;
            var globalJpnFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Japanese Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalJpnFolder.Mark = FolderMark.Locked;
            var globalSpaFolder = Folder.CreatePackageFolder(GlobalPackagePS2, "Spanish Sound Effects", globalAssets, GlobalPackagePS2.Name);
            globalSpaFolder.Mark = FolderMark.Locked;

            assets.Add(globalAssets.URI, globalAssets);
            assets.Add(globalBlendSkinsFolder.URI, globalBlendSkinsFolder);
            assets.Add(globalSkinsFolder.URI, globalSkinsFolder);
            assets.Add(globalModelsFolder.URI, globalModelsFolder);
            assets.Add(globalRigidModelsFolder.URI, globalRigidModelsFolder);
            assets.Add(globalMaterialsFolder.URI, globalMaterialsFolder);
            assets.Add(globalTexturesFolder.URI, globalTexturesFolder);
            assets.Add(globalMeshesFolder.URI, globalMeshesFolder);
            assets.Add(globalAnimationsFolder.URI, globalAnimationsFolder);
            assets.Add(globalCodeModelsFolder.URI, globalCodeModelsFolder);
            assets.Add(globalGameObjectsFolder.URI, globalGameObjectsFolder);
            assets.Add(globalScriptsFolder.URI, globalScriptsFolder);
            assets.Add(globalOgisFolder.URI, globalOgisFolder);
            assets.Add(globalSfxFolder.URI, globalSfxFolder);
            assets.Add(globalEnFolder.URI, globalEnFolder);
            assets.Add(globalFrFolder.URI, globalFrFolder);
            assets.Add(globalGrFolder.URI, globalGrFolder);
            assets.Add(globalItaFolder.URI, globalItaFolder);
            assets.Add(globalJpnFolder.URI, globalJpnFolder);
            assets.Add(globalSpaFolder.URI, globalSpaFolder);

            // Create PS2 package folders for storing assets for display in project tree
            var ps2Package = Ps2Package;
            var ps2BlendSkinsFolder = Folder.CreatePS2Folder("Blend Skins", ps2Package, ps2Package.Name);
            ps2BlendSkinsFolder.Mark = FolderMark.Locked;
            var ps2SkinsFolder = Folder.CreatePS2Folder("Skins", ps2Package, ps2Package.Name);
            ps2SkinsFolder.Mark = FolderMark.Locked;
            var ps2ModelsFolder = Folder.CreatePS2Folder("Models", ps2Package, ps2Package.Name);
            ps2ModelsFolder.Mark = FolderMark.Locked;
            var ps2RigidModelsFolder = Folder.CreatePS2Folder("Rigid models", ps2Package, ps2Package.Name);
            ps2RigidModelsFolder.Mark = FolderMark.Locked;
            var ps2MeshesFolder = Folder.CreatePS2Folder("Meshes", ps2Package, ps2Package.Name);
            ps2MeshesFolder.Mark = FolderMark.Locked;
            var ps2MaterialsFolder = Folder.CreatePS2Folder("Materials", ps2Package, ps2Package.Name);
            ps2MaterialsFolder.Mark = FolderMark.Locked;
            var ps2LodsFolder = Folder.CreatePS2Folder("LODs", ps2Package, ps2Package.Name);
            ps2LodsFolder.Mark = FolderMark.Locked;
            var ps2SkydomesFolder = Folder.CreatePS2Folder("Skydomes", ps2Package, ps2Package.Name);
            ps2SkydomesFolder.Mark = FolderMark.Locked;
            var ps2TexturesFolder = Folder.CreatePS2Folder("Textures", ps2Package, ps2Package.Name);
            ps2TexturesFolder.Mark = FolderMark.Locked;
            var ps2AnimationsFolder = Folder.CreatePS2Folder("Animations", ps2Package, ps2Package.Name);
            ps2AnimationsFolder.Mark = FolderMark.Locked;
            var ps2CodeModelsFolder = Folder.CreatePS2Folder("Code models", ps2Package, ps2Package.Name);
            ps2CodeModelsFolder.Mark = FolderMark.Locked;
            var ps2GameObjectsFolder = Folder.CreatePS2Folder("Game objects", ps2Package, ps2Package.Name);
            ps2GameObjectsFolder.Mark = FolderMark.Locked;
            var ps2ScriptsFolder = Folder.CreatePS2Folder("Scripts", ps2Package, ps2Package.Name);
            ps2ScriptsFolder.Mark = FolderMark.Locked;
            var ps2OgisFolder = Folder.CreatePS2Folder("OGIs/Skeletons", ps2Package, ps2Package.Name);
            ps2OgisFolder.Mark = FolderMark.Locked;
            var ps2SfxFolder = Folder.CreatePS2Folder("Sound Effects", ps2Package, ps2Package.Name);
            ps2SfxFolder.Mark = FolderMark.Locked;
            var ps2EnFolder = Folder.CreatePS2Folder("English Sound Effects", ps2Package, ps2Package.Name);
            ps2EnFolder.Mark = FolderMark.Locked;
            var ps2FrFolder = Folder.CreatePS2Folder("French Sound Effects", ps2Package, ps2Package.Name);
            ps2FrFolder.Mark = FolderMark.Locked;
            var ps2GrFolder = Folder.CreatePS2Folder("German Sound Effects", ps2Package, ps2Package.Name);
            ps2GrFolder.Mark = FolderMark.Locked;
            var ps2ItaFolder = Folder.CreatePS2Folder("Italian Sound Effects", ps2Package, ps2Package.Name);
            ps2ItaFolder.Mark = FolderMark.Locked;
            var ps2JpnFolder = Folder.CreatePS2Folder("Japanese Sound Effects", ps2Package, ps2Package.Name);
            ps2JpnFolder.Mark = FolderMark.Locked;
            var ps2SpaFolder = Folder.CreatePS2Folder("Spanish Sound Effects", ps2Package, ps2Package.Name);
            ps2SpaFolder.Mark = FolderMark.Locked;
            var ps2ChunksFolder = Folder.CreatePS2Folder("Chunks", ps2Package, ps2Package.Name);
            ps2ChunksFolder.Mark = FolderMark.Locked | FolderMark.ChunksOnly;

            assets.Add(ps2BlendSkinsFolder.URI, ps2BlendSkinsFolder);
            assets.Add(ps2SkinsFolder.URI, ps2SkinsFolder);
            assets.Add(ps2ModelsFolder.URI, ps2ModelsFolder);
            assets.Add(ps2RigidModelsFolder.URI, ps2RigidModelsFolder);
            assets.Add(ps2MeshesFolder.URI, ps2MeshesFolder);
            assets.Add(ps2MaterialsFolder.URI, ps2MaterialsFolder);
            assets.Add(ps2LodsFolder.URI, ps2LodsFolder);
            assets.Add(ps2SkydomesFolder.URI, ps2SkydomesFolder);
            assets.Add(ps2TexturesFolder.URI, ps2TexturesFolder);
            assets.Add(ps2AnimationsFolder.URI, ps2AnimationsFolder);
            assets.Add(ps2CodeModelsFolder.URI, ps2CodeModelsFolder);
            assets.Add(ps2GameObjectsFolder.URI, ps2GameObjectsFolder);
            assets.Add(ps2ScriptsFolder.URI, ps2ScriptsFolder);
            assets.Add(ps2OgisFolder.URI, ps2OgisFolder);
            assets.Add(ps2SfxFolder.URI, ps2SfxFolder);
            assets.Add(ps2EnFolder.URI, ps2EnFolder);
            assets.Add(ps2FrFolder.URI, ps2FrFolder);
            assets.Add(ps2GrFolder.URI, ps2GrFolder);
            assets.Add(ps2ItaFolder.URI, ps2ItaFolder);
            assets.Add(ps2JpnFolder.URI, ps2JpnFolder);
            assets.Add(ps2SpaFolder.URI, ps2SpaFolder);
            assets.Add(ps2ChunksFolder.URI, ps2ChunksFolder);
            // Unpack all assets from chunks
            foreach (var item in archive.Items)
            {
                var path = item.Header.Path;
                var pathLow = item.Header.Path.ToLower();
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
                    Folder prevFolder = GlobalPackagePS2;
                    // Create folder hierarchy for global resources
                    for (var i = 1; i < otherFolders.Length - 1; ++i)
                    {
                        var existFolder = prevFolder.GetData().To<FolderData>().Children.FirstOrDefault(c => folders.ContainsKey(c) && folders[c].Name == otherFolders[i], LabURI.Empty);
                        if (existFolder != LabURI.Empty)
                        {
                            prevFolder = folders[existFolder];
                            continue;
                        }
                        Folder nextFolder = Folder.CreatePS2Folder(otherFolders[i], prevFolder);
                        folders.Add(nextFolder.URI, nextFolder);
                        assets.Add(nextFolder.URI, nextFolder);
                        prevFolder = nextFolder;
                    }

                    // Check for text files
                    if (isTxt)
                    {
                        using System.IO.StreamReader textReader = new(ms);
                        var text = textReader.ReadToEnd();
                        var textFile = new TextFile(GlobalPackagePS2.URI, true, pathLow, resourceName, text);
                        assets.Add(textFile.URI, textFile);
                        prevFolder.AddChild(textFile);
                        continue;
                    }

                    using System.IO.BinaryReader globalReader = new(ms);

                    // Check for fonts
                    if (isFont)
                    {
                        var font = new PS2PSF();
                        font.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                        var fontAsset = new Font(GlobalPackagePS2.URI, true, pathLow, resourceName, font);
                        assets.Add(fontAsset.URI, fontAsset);
                        prevFolder.AddChild(fontAsset);
                        continue;
                    }

                    // Check for PSM
                    if (isPsm)
                    {
                        var psm = new PS2PSM();
                        psm.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                        var psmAsset = new PSM(GlobalPackagePS2.URI, true, pathLow, resourceName, psm);
                        assets.Add(psmAsset.URI, psmAsset);
                        prevFolder.AddChild(psmAsset);
                        continue;
                    }

                    // Check for PTC
                    if (isPtc)
                    {
                        var ptc = new PS2PTC();
                        ptc.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                        var ptcAsset = new PTC(GlobalPackagePS2.URI, true, pathLow, resourceName, ptc);
                        assets.Add(ptcAsset.URI, ptcAsset);
                        prevFolder.AddChild(ptcAsset);
                        continue;
                    }

                    // Check for Save Icon
                    if (isIco)
                    {
                        var ico = new SaveIcon(GlobalPackagePS2.URI, false, "", resourceName, item.Data);
                        assets.Add(ico.URI, ico);
                        prevFolder.AddChild(ico);
                        continue;
                    }

                    // Check for frontend (UI sound effects library)
                    if (isFrontend)
                    {
                        var frontend = new PS2Frontend();
                        frontend.Read(globalReader, (Int32)globalReader.BaseStream.Length);
                        var uiLibrary = new UiSoundLibrary(GlobalPackagePS2.URI, false, "", "Frontend", frontend)
                        {
                            Alias = "UI Sound Library"
                        };
                        assets.Add(uiLibrary.URI, uiLibrary);
                        prevFolder.AddChild(uiLibrary);
                        continue;
                    }
                }

                using System.IO.BinaryReader reader = new(ms);

                // Check for chunk file
                if (isRm2 || isSm2)
                {
                    ITwinSection? chunk = null;
                    uint graphicsSectionID = Constants.LEVEL_GRAPHICS_SECTION;
                    if (isDefault)
                    {
                        chunk = new PS2Default();
                    }
                    else if (isRm2)
                    {
                        chunk = new PS2AnyTwinsanityRM2();
                    }
                    else if (isSm2)
                    {
                        chunk = new PS2AnyTwinsanitySM2();
                        graphicsSectionID = Constants.SCENERY_GRAPHICS_SECTION;
                    }
                    // Fill chunk data
                    chunk!.Read(reader, (Int32)ms.Length);

                    // Pick appropriate folders
                    var blendSkinsFolder = isDefault ? globalBlendSkinsFolder : ps2BlendSkinsFolder;
                    var skinsFolder = isDefault ? globalSkinsFolder : ps2SkinsFolder;
                    var modelsFolder = isDefault ? globalModelsFolder : ps2ModelsFolder;
                    var rigidModelsFolder = isDefault ? globalRigidModelsFolder : ps2RigidModelsFolder;
                    var meshesFolder = isDefault ? globalMeshesFolder : ps2MeshesFolder;
                    var materialsFolder = isDefault ? globalMaterialsFolder : ps2MaterialsFolder;
                    var lodsFolder = ps2LodsFolder;
                    var skydomesFolder = ps2SkydomesFolder;
                    var texturesFolder = isDefault ? globalTexturesFolder : ps2TexturesFolder;
                    var animationsFolder = isDefault ? globalAnimationsFolder : ps2AnimationsFolder;
                    var codeModelsFolder = isDefault ? globalCodeModelsFolder : ps2CodeModelsFolder;
                    var gameObjectsFolder = isDefault ? globalGameObjectsFolder : ps2GameObjectsFolder;
                    var scriptsFolder = isDefault ? globalScriptsFolder : ps2ScriptsFolder;
                    var ogisFolder = isDefault ? globalOgisFolder : ps2OgisFolder;
                    var sfxFolder = isDefault ? globalSfxFolder : ps2SfxFolder;
                    var enFolder = isDefault ? globalEnFolder : ps2EnFolder;
                    var frFolder = isDefault ? globalFrFolder : ps2FrFolder;
                    var grFolder = isDefault ? globalGrFolder : ps2GrFolder;
                    var itaFolder = isDefault ? globalItaFolder : ps2ItaFolder;
                    var jpnFolder = isDefault ? globalJpnFolder : ps2JpnFolder;
                    var spaFolder = isDefault ? globalSpaFolder : ps2SpaFolder;
                    var chunksFolder = ps2ChunksFolder;

                    // Chunk's path for variation of resources if they happen to be duplicated
                    var chunkPath = path;
                    if (isDefault)
                    {
                        GlobalPackagePS2.Variant = chunkPath;
                    }
                    path = path[..^4];

                    // Read graphics stuff
                    var graphics = chunk.GetItem<PS2AnyGraphicsSection>(graphicsSectionID);
                    ReadSectionItems<Texture, PS2AnyTexturesSection, PS2AnyTexture>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_TEXTURES_SECTION, texturesFolder);
                    ReadSectionItems<Material, PS2AnyMaterialsSection, PS2AnyMaterial>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_MATERIALS_SECTION, materialsFolder);
                    ReadSectionItems<Model, PS2AnyModelsSection, PS2AnyModel>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_MODELS_SECTION, modelsFolder);
                    ReadSectionItems<RigidModel, PS2AnyRigidModelsSection, PS2AnyRigidModel>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_RIGID_MODELS_SECTION, rigidModelsFolder);
                    ReadSectionItems<Skin, PS2AnySkinsSection, PS2AnySkin>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_SKINS_SECTION, skinsFolder);
                    ReadSectionItems<BlendSkin, PS2AnyBlendSkinsSection, PS2AnyBlendSkin>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_BLEND_SKINS_SECTION, blendSkinsFolder);
                    ReadSectionItems<Mesh, PS2AnyMeshesSection, PS2AnyMesh>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_MESHES_SECTION, meshesFolder);
                    ReadSectionItems<LodModel, PS2AnyLODsSection, PS2AnyLOD>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_LODS_SECTION, lodsFolder);
                    ReadSectionItems<Skydome, PS2AnySkydomesSection, PS2AnySkydome>
                        (assets, graphics, chunkPath, graphicsCheck, Constants.GRAPHICS_SKYDOMES_SECTION, skydomesFolder);

                    // Read code stuff
                    var code = chunk.GetItem<PS2AnyCodeSection>(Constants.LEVEL_CODE_SECTION);
                    if (code != null)
                    {
                        ReadSectionItems<GameObject, PS2AnyGameObjectsSection, PS2AnyObject>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_GAME_OBJECTS_SECTION, gameObjectsFolder);
                        ReadSectionItems<Animation, PS2AnyAnimationsSection, PS2AnyAnimation>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_ANIMATIONS_SECTION, animationsFolder);
                        ReadSectionItems<OGI, PS2AnyOGIsSection, PS2AnyOGI>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_OGIS_SECTION, ogisFolder);
                        ReadSectionItems<BehaviourCommandsSequence, PS2AnyBehaviourCommandsSequencesSection, PS2BehaviourCommandsSequence>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION, codeModelsFolder);

                        // Behaviours are special because they are ID oddity dependent
                        var items = code.GetItem<PS2AnyBehavioursSection>(Constants.CODE_BEHAVIOURS_SECTION);
                        for (var i = 0; i < items.GetItemsAmount(); ++i)
                        {
                            var asset = items.GetItem<TwinBehaviourWrapper>(items.GetItem(i).GetID());
                            var behaviourChecker = codeCheck[Constants.CODE_BEHAVIOURS_SECTION];
                            var hasHash = behaviourChecker.ContainsKey(asset.GetHash());
                            if (hasHash && !isDefault)
                            {
                                if (!behaviourChecker.ContainsValue(asset.GetID()))
                                {
                                    throw new Exception($"HASH COLLISION FOR ASSET {asset.GetName()} WITH ID {asset.GetID()}");
                                }
                                continue;
                            }
                            if (!hasHash)
                            {
                                behaviourChecker.Add(asset.GetHash(), asset.GetID());
                            }
                            // If hash was unique but Twinsanity's ID wasn't then we will mark it with a variant which is gonna be chunk's name
                            var needVariant = behaviourChecker.Values.Count(e => e == asset.GetID()) > 1 || isDefault;
                            var isHeader = asset.GetID() % 2 == 0;
                            var type = isHeader ? typeof(BehaviourStarter) : typeof(BehaviourGraph);
                            if (needVariant)
                            {
                                // TODO: Add dupes addition
                            }
                            var package = isDefault ? GlobalPackagePS2 : ps2Package;
                            var metaAsset = (IAsset?)Activator.CreateInstance(type, package.URI, needVariant, chunkPath,
                                asset.GetID(), asset.GetName(), asset) ?? throw new ProjectException($"Could not read asset {asset.GetName()} with ID {asset.GetID()}");
                            if (!isHeader)
                            {
                                scriptsFolder.AddChild(metaAsset);
                            }
                            assets.Add(metaAsset.URI, metaAsset);
                        }

                        ReadSectionItems<SoundEffect, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_SOUND_EFFECTS_SECTION, sfxFolder);
                        ReadSectionItems<SoundEffectSP, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_SPA_SECTION, spaFolder);
                        ReadSectionItems<SoundEffectJP, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_JPN_SECTION, jpnFolder);
                        ReadSectionItems<SoundEffectIT, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_ITA_SECTION, itaFolder);
                        ReadSectionItems<SoundEffectGR, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_GER_SECTION, grFolder);
                        ReadSectionItems<SoundEffectFR, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_FRE_SECTION, frFolder);
                        ReadSectionItems<SoundEffectEN, PS2AnySoundsSection, PS2AnySound>
                            (assets, code, chunkPath, codeCheck, Constants.CODE_LANG_ENG_SECTION, enFolder);
                    }

                    // Chunk folder
                    var otherFolders = path.Split(System.IO.Path.DirectorySeparatorChar);
                    Folder prevFolder = chunksFolder;
                    // Create chunk folder hierarchy
                    if (!isDefault)
                    {
                        for (var i = 1; i < otherFolders.Length; ++i)
                        {
                            var existFolder = prevFolder.GetData().To<FolderData>().Children.FirstOrDefault(c => folders.ContainsKey(c) && folders[c].Name == otherFolders[i], LabURI.Empty);
                            if (existFolder != LabURI.Empty)
                            {
                                prevFolder = folders[existFolder];
                                continue;
                            }
                            Folder nextFolder;
                            if (i != otherFolders.Length - 1)
                            {
                                nextFolder = Folder.CreatePS2Folder(otherFolders[i], prevFolder, chunkPath);
                            }
                            else
                            {
                                nextFolder = new ChunkFolder(ps2Package.URI, otherFolders[i], prevFolder, chunkPath);
                            }
                            folders.Add(nextFolder.URI, nextFolder);
                            assets.Add(nextFolder.URI, nextFolder);
                            prevFolder = nextFolder;
                        }
                    }
                    else
                    {
                        prevFolder = new ChunkFolder(GlobalPackagePS2.URI, "Default", GlobalPackagePS2, GlobalPackagePS2.Name);
                        prevFolder.Mark = FolderMark.DefaultOnly;
                        assets.Add(prevFolder.URI, prevFolder);
                    }
                    var chunkFolder = prevFolder;

                    // RM2 per chunk instances
                    if (isRm2)
                    {
                        if (!isDefault)
                        {
                            // Extract collision data
                            var collisionData = chunk.GetItem<PS2AnyCollisionData>(Constants.LEVEL_COLLISION_ITEM);
                            var colData = new Collision(ps2Package.URI, collisionData.GetID(), collisionData.GetName(), chunkPath, collisionData);
                            assets.Add(colData.URI, colData);
                            chunkFolder.AddChild(colData);

                            var particleData = chunk.GetItem<PS2AnyParticleData>(Constants.LEVEL_PARTICLES_ITEM);
                            var partData = new Particles(ps2Package.URI, particleData.GetID(), particleData.GetName(), chunkPath, particleData);
                            assets.Add(partData.URI, partData);
                            chunkFolder.AddChild(partData);
                        }
                        else
                        {
                            var particleData = chunk.GetItem<PS2DefaultParticleData>(Constants.LEVEL_PARTICLES_ITEM);
                            var partData = new DefaultParticles(GlobalPackagePS2.URI, particleData.GetID(), particleData.GetName(), chunkPath, particleData);
                            assets.Add(partData.URI, partData);
                            chunkFolder.AddChild(partData);
                        }

                        // Instance layout
                        var instancePackage = isDefault ? GlobalPackagePS2 : ps2Package;
                        var instFolder = Folder.CreatePackageFolder(instancePackage, "Instances", chunkFolder, chunkPath);
                        instFolder.Mark = FolderMark.InChunk;
                        var aiPathFolder = Folder.CreatePackageFolder(instancePackage, "AI Navigation Paths", chunkFolder, chunkPath);
                        aiPathFolder.Mark = FolderMark.InChunk;
                        var aiPosFolder = Folder.CreatePackageFolder(instancePackage, "AI Navigation Positions", chunkFolder, chunkPath);
                        aiPosFolder.Mark = FolderMark.InChunk;
                        var cameraFolder = Folder.CreatePackageFolder(instancePackage, "Cameras", chunkFolder, chunkPath);
                        cameraFolder.Mark = FolderMark.InChunk;
                        var colSurfaceFolder = Folder.CreatePackageFolder(instancePackage, "Collision Surfaces", chunkFolder, chunkPath);
                        colSurfaceFolder.Mark = FolderMark.InChunk;
                        var instTempFolder = Folder.CreatePackageFolder(instancePackage, "Instance Templates", chunkFolder, chunkPath);
                        instTempFolder.Mark = FolderMark.InChunk;
                        var pathFolder = Folder.CreatePackageFolder(instancePackage, "Paths", chunkFolder, chunkPath);
                        pathFolder.Mark = FolderMark.InChunk;
                        var posFolder = Folder.CreatePackageFolder(instancePackage, "Positions", chunkFolder, chunkPath);
                        posFolder.Mark = FolderMark.InChunk;
                        var trgFolder = Folder.CreatePackageFolder(instancePackage, "Triggers", chunkFolder, chunkPath);
                        trgFolder.Mark = FolderMark.InChunk;
                        assets.Add(instFolder.URI, instFolder);
                        assets.Add(aiPathFolder.URI, aiPathFolder);
                        assets.Add(aiPosFolder.URI, aiPosFolder);
                        assets.Add(cameraFolder.URI, cameraFolder);
                        assets.Add(colSurfaceFolder.URI, colSurfaceFolder);
                        assets.Add(instTempFolder.URI, instTempFolder);
                        assets.Add(pathFolder.URI, pathFolder);
                        assets.Add(posFolder.URI, posFolder);
                        assets.Add(trgFolder.URI, trgFolder);

                        for (var i = 0; i < 8; ++i)
                        {
                            var layId = Constants.LEVEL_LAYOUT_1_SECTION + i;
                            var layout = chunk.GetItem<PS2AnyLayoutSection>((UInt32)layId);
                            ReadSectionItems<ObjectInstance, PS2AnyInstancesSection, PS2AnyInstance>
                                (assets, layout, Constants.LAYOUT_INSTANCES_SECTION, chunkPath, layId, instFolder);
                            ReadSectionItems<AiPath, PS2AnyAIPathsSection, PS2AnyAIPath>
                                (assets, layout, Constants.LAYOUT_AI_PATHS_SECTION, chunkPath, layId, aiPathFolder);
                            ReadSectionItems<AiPosition, PS2AnyAIPositionsSection, PS2AnyAIPosition>
                                (assets, layout, Constants.LAYOUT_AI_POSITIONS_SECTION, chunkPath, layId, aiPosFolder);
                            ReadSectionItems<Camera, PS2AnyCamerasSection, PS2AnyCamera>
                                (assets, layout, Constants.LAYOUT_CAMERAS_SECTION, chunkPath, layId, cameraFolder);
                            ReadSectionItems<CollisionSurface, PS2AnySurfacesSection, PS2AnyCollisionSurface>
                                (assets, layout, Constants.LAYOUT_SURFACES_SECTION, chunkPath, layId, colSurfaceFolder);
                            ReadSectionItems<InstanceTemplate, PS2AnyTemplatesSection, PS2AnyTemplate>
                                (assets, layout, Constants.LAYOUT_TEMPLATES_SECTION, chunkPath, layId, instTempFolder);
                            ReadSectionItems<Path, PS2AnyPathsSection, PS2AnyPath>
                                (assets, layout, Constants.LAYOUT_PATHS_SECTION, chunkPath, layId, pathFolder);
                            ReadSectionItems<Position, PS2AnyPositionsSection, PS2AnyPosition>
                                (assets, layout, Constants.LAYOUT_POSITIONS_SECTION, chunkPath, layId, posFolder);
                            ReadSectionItems<Trigger, PS2AnyTriggersSection, PS2AnyTrigger>
                                (assets, layout, Constants.LAYOUT_TRIGGERS_SECTION, chunkPath, layId, trgFolder);
                        }
                    }

                    // SM2 per chunk instances
                    if (isSm2)
                    {
                        var scenery = chunk.GetItem<PS2AnyScenery>(Constants.SCENERY_SECENERY_ITEM);
                        var dynamicScenery = chunk.GetItem<PS2AnyDynamicScenery>(Constants.SCENERY_DYNAMIC_SECENERY_ITEM);
                        var chunkLinks = chunk.GetItem<PS2AnyLink>(Constants.SCENERY_LINK_ITEM);
                        var sceneryAsset = new Scenery(ps2Package.URI, scenery.GetID(), scenery.GetName(), chunkPath, scenery);
                        var dynamicSceneryAsset = new DynamicScenery(ps2Package.URI, dynamicScenery.GetID(), dynamicScenery.GetName(), chunkPath, dynamicScenery);
                        var chunkLinksAsset = new ChunkLinks(ps2Package.URI, chunkLinks.GetID(), chunkLinks.GetName(), chunkPath, chunkLinks);
                        assets.Add(sceneryAsset.URI, sceneryAsset);
                        assets.Add(dynamicSceneryAsset.URI, dynamicSceneryAsset);
                        assets.Add(chunkLinksAsset.URI, chunkLinksAsset);
                        chunkFolder.AddChild(sceneryAsset);
                        chunkFolder.AddChild(dynamicSceneryAsset);
                        chunkFolder.AddChild(chunkLinksAsset);
                    }
                }
            }

            Log.WriteLine("Adding unpacked assets into asset manager...");
            AssetManager.AddAllAssets(assets);
            // Add any additional assets that are stored internally and generate additional URIs that AssetManager needs to know about
            foreach (var uri in globalCodeModelsFolder.GetData().To<FolderData>().Children)
            {
                var cm = AssetManager.GetAsset<BehaviourCommandsSequence>(uri);
                foreach (var sequence in cm.BehaviourGraphLinks)
                {
                    AssetManager.AddAssetUnsafe(sequence.Value, cm);
                }
            }
            foreach (var uri in ps2CodeModelsFolder.GetData().To<FolderData>().Children)
            {
                var cm = AssetManager.GetAsset<BehaviourCommandsSequence>(uri);
                foreach (var sequence in cm.BehaviourGraphLinks)
                {
                    AssetManager.AddAssetUnsafe(sequence.Value, cm);
                }
            }
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
            var chunk = AssetManager.Get().GetAsset(chunkUri);
            System.IO.Directory.SetCurrentDirectory(ProjectPath);
            System.IO.Directory.CreateDirectory("build");
            System.IO.Directory.SetCurrentDirectory("build");
            System.IO.Directory.CreateDirectory("archives");
            System.IO.Directory.SetCurrentDirectory("archives");
            if (chunk.Name == "Default")
            {
                Log.WriteLine("Writing Default chunk...");
                System.IO.Directory.CreateDirectory("Startup");
                System.IO.Directory.SetCurrentDirectory("Startup");
                var @default = factory.GenerateDefault();
                chunk.ResolveChunkResources(factory, @default);
                // Default is a special case where we need to put in the meshes which are drop shadows
                var defaultMeshes = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                                     let asset = assetManager.GetAsset(assetUri)
                                     where asset is Folder
                                     where asset.Name.Contains("Global Assets")
                                     from childUri in asset.GetData<FolderData>().Children
                                     let child = assetManager.GetAsset(childUri)
                                     where child is Folder
                                     where child.Name.Contains("Meshes")
                                     select child).First();
                defaultMeshes.ResolveChunkResources(factory, @default.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION));
                var defaultModels = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                    let asset = assetManager.GetAsset(assetUri)
                    where asset is Folder
                    where asset.Name.Contains("Global Assets")
                    from childUri in asset.GetData<FolderData>().Children
                    let child = assetManager.GetAsset(childUri)
                    where child is Folder
                    where child.Name.Contains("Models")
                    select child).First();
                var modelsSection = @default.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION)
                    .GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);
                var itemsAmount = modelsSection.GetItemsAmount();
                for (var i = itemsAmount - 1; i >= 0; --i)
                {
                    var item = modelsSection.GetItem(i);
                    modelsSection.RemoveItem<ITwinItem>(item.GetID());
                }
                defaultModels.ResolveChunkResources(factory, modelsSection);
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
            var chunkLevelPath = chunk.Variation;
            foreach (var pathToken in chunkLevelPath.Split('\\').Skip(1).SkipLast(1))
            {
                System.IO.Directory.CreateDirectory(pathToken.Replace("\\", ""));
                System.IO.Directory.SetCurrentDirectory(pathToken.Replace("\\", ""));
            }
            Log.WriteLine($"Writing Level {chunk.Alias}...");
            var rm2 = factory.GenerateRM();
            var sm2 = factory.GenerateSM();

            foreach (var asset in chunk.GetData<FolderData>().Children.Select(child => assetManager.GetAsset(child)))
            {
                if (asset is Scenery or DynamicScenery or ChunkLinks)
                {
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
                Log.WriteLine("Error: Global PS2 package MUST be enabled to compile the project");
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
            Log.WriteLine("Writing Levels...");
            var chunksFolder = (from dependencyUri in BasePackage.Dependencies
                                let dependency = assetManager.GetAsset<Package>(dependencyUri)
                                where dependency.Enabled
                                let foldersInPackage = ((IAsset)dependency).GetData<FolderData>().Children
                                from folderUri in foldersInPackage
                                let folder = assetManager.GetAsset(folderUri)
                                where folder.Name == "Chunks"
                                select folder.GetData<FolderData>()).ToList();
            UInt32 totalGlobals = 0;
            UInt32 currentGlobalsCount = 0;
            foreach (var folder in chunksFolder)
            {
                ResolveAndWriteChunks(factory, folder, ref totalGlobals, ref currentGlobalsCount);
            }

            Log.WriteLine("Writing Extras...");
            System.IO.Directory.SetCurrentDirectory("../Extras");

            var extrasFolders = (from asset in GlobalPackagePS2.GetData().To<FolderData>().Children
                                 where assetManager.GetAsset(asset) is Folder
                                 let folder = assetManager.GetAsset<Folder>(asset)
                                 where ArchivesLayout.ExtrasFolders.Contains(folder.Name)
                                 select asset).ToList();
            var mcdonaldsAssset = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                                   let asset = assetManager.GetAsset(assetUri)
                                   where asset.Name == "McDonalds01"
                                   select asset).First();

            Log.WriteLine($"Writing ({++currentGlobalsCount}/{++totalGlobals}) {mcdonaldsAssset.Name}...");
            mcdonaldsAssset.ExportToFile(factory);

            ResolveGlobalAssets(factory, extrasFolders, ref totalGlobals, ref currentGlobalsCount);

            System.IO.Directory.SetCurrentDirectory("../Language");
            Log.WriteLine("Writing Language...");
            var languageFolders = (from asset in GlobalPackagePS2.GetData().To<FolderData>().Children
                                   where assetManager.GetAsset(asset) is Folder
                                   let folder = assetManager.GetAsset<Folder>(asset)
                                   where ArchivesLayout.LanguageFolder.Contains(folder.Name)
                                   select asset).ToList();

            ResolveGlobalAssets(factory, languageFolders, ref totalGlobals, ref currentGlobalsCount);

            System.IO.Directory.SetCurrentDirectory("../Startup");
            Log.WriteLine("Writing Startup...");
            var startupAssets = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                                 let asset = assetManager.GetAsset(assetUri)
                                 where asset is not ChunkFolder
                                 where ArchivesLayout.StartupItems.Contains(asset.Name)
                                 select assetUri).ToList();

            var defaultChunk = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                                let asset = assetManager.GetAsset(assetUri)
                                where asset is ChunkFolder
                                where ArchivesLayout.StartupItems.Contains(asset.Name)
                                select asset).First();
            Log.WriteLine($"Writing ({++currentGlobalsCount}/{++totalGlobals}) default...");
            var @default = factory.GenerateDefault();
            defaultChunk.ResolveChunkResources(factory, @default);
            // Default is a special case where we need to put in the meshes which are drop shadows
            var defaultMeshes = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                                 let asset = assetManager.GetAsset(assetUri)
                                 where asset is Folder
                                 where asset.Name.Contains("Global Assets")
                                 from childUri in asset.GetData<FolderData>().Children
                                 let child = assetManager.GetAsset(childUri)
                                 where child is Folder
                                 where child.Name.Contains("Meshes")
                                 select child).First();
            defaultMeshes.ResolveChunkResources(factory, @default.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION).GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION));
            var defaultModels = (from assetUri in GlobalPackagePS2.GetData().To<FolderData>().Children
                let asset = assetManager.GetAsset(assetUri)
                where asset is Folder
                where asset.Name.Contains("Global Assets")
                from childUri in asset.GetData<FolderData>().Children
                let child = assetManager.GetAsset(childUri)
                where child is Folder
                where child.Name.Contains("Models")
                select child).First();
            var modelsSection = @default.GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION)
                .GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);
            var itemsAmount = modelsSection.GetItemsAmount();
            for (var i = itemsAmount - 1; i >= 0; --i)
            {
                var item = modelsSection.GetItem(i);
                modelsSection.RemoveItem<ITwinItem>(item.GetID());
            }
            defaultModels.ResolveChunkResources(factory, modelsSection);
            using var defaultFile = new System.IO.FileStream($"Default.rm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var defaultWriter = new System.IO.BinaryWriter(defaultFile);
            @default.Write(defaultWriter);
            defaultWriter.Flush();
            defaultWriter.Close();

            ResolveGlobalAssets(factory, startupAssets, ref totalGlobals, ref currentGlobalsCount);

            System.IO.Directory.SetCurrentDirectory("../..");
            Log.WriteLine("Finished writing main archive files!");
            CreatePs2ArchivesAndIso();
        }

        public void CreatePs2ArchivesAndIso()
        {
            Log.WriteLine("Packing into BD/BH archives...");
            var bd = new PS2BD("", $"{DiscContentPathPS2}\\Crash6\\Crash.BH");
            using var bdFile = new System.IO.FileStream($"{DiscContentPathPS2}\\Crash6\\Crash.BD", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var bdWriter = new System.IO.BinaryWriter(bdFile);
            bd.BuildRecords($"{ProjectPath}\\build\\archives");
            bd.Write(bdWriter);
            bdWriter.Flush();
            bdWriter.Close();
            
            Log.WriteLine("Creating PS2 ISO image...");
            var progress = Ps2ImageMaker.StartPacking(DiscContentPathPS2!, $"{ProjectPath}\\build\\image\\{Name}.iso");
            while (!progress.Finished)
            {
                Thread.Sleep(TimeSpan.FromSeconds(0.5));
                progress = Ps2ImageMaker.PollProgress();
                Log.WriteLine($"ISO creating progress {progress.ProgressPercentage * 100:F2}%...");
            }
            Log.WriteLine($"Finished creating the ISO! Check the {ProjectPath}\\build\\image folder!");
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
                var folder = assetManager.GetAsset(item);
                if (folder is not Folder)
                {
                    currentGlobalsCount++;
                    Log.WriteLine($"Writing ({currentGlobalsCount}/{totalGlobals}) {folder.Name}...");
                    folder.ExportToFile(factory);
                    continue;
                }

                System.IO.Directory.CreateDirectory(folder.Name);
                System.IO.Directory.SetCurrentDirectory(folder.Name);
                ResolveGlobalAssets(factory, folder.GetData<FolderData>().Children, ref totalGlobals, ref currentGlobalsCount);
                System.IO.Directory.SetCurrentDirectory("..");
            }
        }

        private void ResolveAndWriteChunks(ITwinItemFactory factory, FolderData currentFolder, ref UInt32 scenesTotal, ref UInt32 currentSceneCount)
        {
            var assetManager = AssetManager.Get();
            scenesTotal += (UInt32)currentFolder.Children.Select(assetManager.GetAsset).Count(a => a is ChunkFolder);
            foreach (var item in currentFolder.Children)
            {
                var folder = assetManager.GetAsset(item);
                if (folder is ChunkFolder)
                {
                    currentSceneCount++;
                    Log.WriteLine($"Writing level ({currentSceneCount}/{scenesTotal}) {folder.Name}...");
                    var rm2 = factory.GenerateRM();
                    var sm2 = factory.GenerateSM();

                    foreach (var asset in folder.GetData<FolderData>().Children.Select(child => assetManager.GetAsset(child)))
                    {
                        if (asset is Scenery or DynamicScenery or ChunkLinks)
                        {
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

                    using var rm2File = new System.IO.FileStream($"{folder.Name}.rm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    using var rm2Writer = new System.IO.BinaryWriter(rm2File);
                    rm2.Write(rm2Writer);
                    rm2Writer.Flush();
                    rm2Writer.Close();

                    using var sm2File = new System.IO.FileStream($"{folder.Name}.sm2", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    using var sm2Writer = new System.IO.BinaryWriter(sm2File);
                    sm2.Write(sm2Writer);
                    sm2Writer.Flush();
                    sm2Writer.Close();
                }
                else if (folder is Folder)
                {
                    System.IO.Directory.CreateDirectory(folder.Name);
                    System.IO.Directory.SetCurrentDirectory(folder.Name);
                    ResolveAndWriteChunks(factory, folder.GetData<FolderData>(), ref scenesTotal, ref currentSceneCount);
                    System.IO.Directory.SetCurrentDirectory("..");
                }
            }
        }


        /// <summary>
        /// Reads items from a section and converts them into project assets
        /// </summary>
        /// <typeparam name="T">Project asset type</typeparam>
        /// <typeparam name="S">Section type</typeparam>
        /// <typeparam name="I">Game asset type</typeparam>
        /// <param name="fromSection">Which section to read from</param>
        /// <param name="globalCheck">Dictionary of global resources to check against</param>
        /// <param name="secId">Subsection ID where game asset is stored at</param>
        private void ReadSectionItems<T, S, I>(Dictionary<LabURI, IAsset> assets, ITwinSection fromSection, String chunkName, Dictionary<uint, Dictionary<String, uint>> globalCheck, uint secId, Folder folder)
            where T : IAsset where S : ITwinSection where I : ITwinItem
        {
            var items = fromSection.GetItem<S>(secId);
            for (var i = 0; i < items.GetItemsAmount(); ++i)
            {
                var asset = items.GetItem<I>(items.GetItem(i).GetID());
                var checker = globalCheck[secId];
                var hasHash = checker.ContainsKey(asset.GetHash());
                var isDefault = chunkName.ToLower().Contains("default");
                // LODS suck because their hashes can easily collide
                if (hasHash && !isDefault && secId != Constants.GRAPHICS_LODS_SECTION)
                {
                    if (!checker.ContainsValue(asset.GetID()))
                    {
                        throw new Exception($"HASH COLLISION FOR ASSET {asset.GetName()} WITH ID {asset.GetID()}");
                    }
                    continue;
                }
                if (!hasHash && !isDefault)
                {
                    checker.Add(asset.GetHash(), asset.GetID());
                }

                // If hash was unique but Twinsanity's ID wasn't then we will mark it with a variant which is gonna be chunk's name
                var needVariant = checker.Values.Count(e => e == asset.GetID()) > 1 || isDefault || secId == Constants.GRAPHICS_LODS_SECTION;
                if (needVariant)
                {
                    //Log.WriteLine($"Found duplicate Twinsanity ID for {typeof(T).Name} {asset.GetName()} in chunk {chunkName}");
                    // TODO: Add dupes addition
                }
                var package = isDefault ? GlobalPackagePS2 : Ps2Package;
                var metaAsset = (T?)Activator.CreateInstance(typeof(T), package.URI, needVariant, chunkName, asset.GetID(), asset.GetName(), asset) ?? throw new ProjectException($"Could not read asset {asset.GetName()} with ID {asset.GetID()}");
                folder.AddChild(metaAsset);
                assets.Add(metaAsset.URI, metaAsset);
            }
        }

        /// <summary>
        /// Reads items from a section and converts them into project assets.
        /// This is primarily used for instances in chunks which are not unique and belong to chunk specifically.
        /// </summary>
        /// <typeparam name="T">Project asset type</typeparam>
        /// <typeparam name="S">Section type</typeparam>
        /// <typeparam name="I">Game asset type</typeparam>
        /// <param name="fromSection">Which section to read from</param>
        /// <param name="secId">Subsection ID where game asset is stored at</param>
        private void ReadSectionItems<T, S, I>(Dictionary<LabURI, IAsset> assets, ITwinSection fromSection, uint secId, string chunkName, int layId, Folder folder)
            where T : IAsset where S : ITwinSection where I : ITwinItem
        {
            var items = fromSection.GetItem<S>(secId);
            if (items != null)
            {
                for (var i = 0; i < items.GetItemsAmount(); ++i)
                {
                    var asset = items.GetItem<I>(items.GetItem(i).GetID());
                    var package = chunkName.ToLower().Contains("default") ? GlobalPackagePS2 : Ps2Package;
                    var metaAsset = (T?)Activator.CreateInstance(typeof(T), package.URI, asset.GetID(), asset.GetName(), chunkName, layId, asset) ?? throw new ProjectException($"Could not read asset {asset.GetName()} with ID {asset.GetID()}");
                    folder.AddChild(metaAsset);
                    assets.Add(metaAsset.URI, metaAsset);
                }
            }
        }
    }
}
