using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Splat;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetData.Code
{
    [ReferencesAssets]
    public class GameObjectData : AbstractAssetData
    {
        public GameObjectData(IAsset asset) : base(asset)
        {
            Name = "NewGameObject";
            Type = ITwinObject.ObjectType.GenericObject;
            UnkTypeValue = 1;
            TriggerBehaviours = new List<ObjectTriggerBehaviourData>();
            ModelSlots = new List<ModelSlot>();
            BehaviourSlots = new List<LabURI>();
            ObjectSlots = new List<LabURI>();
            SoundSlots = new List<LabURI>();
            InstFlags = new List<UInt32>();
            InstFloats = new List<float>();
            InstIntegers = new List<UInt32>();
            RefObjects = new List<LabURI>();
            RefBehaviours = new List<LabURI>();
            RefSounds = new List<LabURI>();
            RefAnimations = new List<UInt16>();
            RefOGIs = new List<LabURI>();
            RefBehaviourCommandsSequences = new List<LabURI>();
            GraphsWithoutStarter = new List<LabURI>();
            BehaviourPack = string.Empty;
        }

        public GameObjectData(IAsset asset, ITwinObject gameObject, Dictionary<string, TwinBehaviourStarter> starterMap) : base(asset)
        {
            _starterMap = starterMap;
            SetTwinItem(gameObject);
        }

        public GameObjectData(IAsset asset, String path) : base(asset) => Load(path, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        });

        [JsonProperty(Required = Required.Always)]
        [Editable]
        public ITwinObject.ObjectType Type { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Hint = "CAREFUL OF EDITING THIS VALUE! In general for all objects it should be equal to 1! For Pickups and Projectiles it can be equal to 17 or 18")]
        public Byte UnkTypeValue { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        public Byte CameraReactJointAmount { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        public Byte ExitPointAmount { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [NoChunkOverrides]
        public String Name { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Trigger Messages", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Message")]
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<ObjectTriggerBehaviourData> TriggerBehaviours { get; set; }
        
        /// <summary>
        /// Models the object can show, each with the animation it plays on it
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Model Slots", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Model Slot")]
        public List<ModelSlot> ModelSlots { get; set; }

        public IReadOnlyList<LabURI> OGISlots => ModelSlots.Select(slot => slot.Ogi).ToList();
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Behaviour Slots", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Behaviour Slot")]
        [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
        [OnReferenceDeleted(DeletedReferenceAction.Clear)]
        public List<LabURI> BehaviourSlots { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Object Slots", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Object Slot")]
        [EditorParam(UriLinkViewModel.BrowseType, typeof(GameObject))]
        [OnReferenceDeleted(DeletedReferenceAction.Clear)]
        public List<LabURI> ObjectSlots { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Sound Slots", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Sound Slot")]
        [EditorParam(UriLinkViewModel.BrowseType, typeof(SoundEffect))]
        [OnReferenceDeleted(DeletedReferenceAction.Clear)]
        public List<LabURI> SoundSlots { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        public Enums.InstanceState InstanceStateFlags { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Template Instance Flags", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Flag")]
        [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
        public List<UInt32> InstFlags { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Template Instance Floats", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Float")]
        [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
        public List<Single> InstFloats { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Template Instance Integers", EditorOrientation = Avalonia.Controls.Dock.Top)]
        [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Integer")]
        [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
        public List<UInt32> InstIntegers { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        [Editable(Caption = "Object's AgentLab Commands", EditorOrientation = Avalonia.Controls.Dock.Top, EditorDescType = typeof(CodeEditorDesc))]
        [EditorParam(DocumentModelViewModel.EditorExplicitOrder, Int32.MaxValue)]
        [EditorParam(CodeEditorViewModel.ValidateAgentLabCode, true)]
        [EditorParam(CodeEditorViewModel.AgentLabCommandsOnly, true)]
        public string BehaviourPack { get; set; }
        
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RefObjects { get; set; }
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RefOGIs { get; set; }
        /// <summary>
        /// Game IDs of the animations of the object and everything it references, worked out when building
        /// </summary>
        public List<UInt16> RefAnimations { get; set; }
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RefBehaviourCommandsSequences { get; set; }
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RefBehaviours { get; set; }
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> RefSounds { get; set; }
        // Graphs the object references without their starter. A starter makes the game run its graph on its own, the game only has a
        // graph's starter in the chunks with objects referencing it
        [JsonProperty]
        [OnReferenceDeleted(DeletedReferenceAction.Remove)]
        public List<LabURI> GraphsWithoutStarter { get; set; } = new();
        // Off for the game's copies of the startup chunk's objects in levels, which don't list what they use. Chunks keep the game's values
        // as their own, startup objects other levels get are copies (ListsResources)
        [JsonProperty]
        public Boolean ReferencesResources { get; set; } = true;
        
        

        protected override void Dispose(Boolean disposing)
        {
            TriggerBehaviours.Clear();
            ModelSlots.Clear();
            BehaviourSlots.Clear();
            ObjectSlots.Clear();
            SoundSlots.Clear();
            InstFlags.Clear();
            InstFloats.Clear();
            InstIntegers.Clear();
            RefObjects.Clear();
            RefOGIs.Clear();
            RefAnimations.Clear();
            RefBehaviourCommandsSequences.Clear();
            RefBehaviours.Clear();
            RefSounds.Clear();
            GraphsWithoutStarter.Clear();
        }

        private Dictionary<string, TwinBehaviourStarter> _starterMap;
        private List<UInt16>? _animationExportIds;
        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var assetManager = AssetManager.Get();
            ITwinObject gameObject = GetTwinItem<ITwinObject>();
            Type = gameObject.Type;
            UnkTypeValue = gameObject.UnkTypeValue;
            CameraReactJointAmount = gameObject.ReactJointAmount;
            ExitPointAmount = gameObject.ExitPointAmount;
            Name = new String(gameObject.Name.ToCharArray());
            TriggerBehaviours = new List<ObjectTriggerBehaviourData>();
            foreach (var e in gameObject.TriggerBehaviours)
            {
                TriggerBehaviours.Add(new ObjectTriggerBehaviourData(Owner, e, _starterMap));
            }
            ModelSlots = new List<ModelSlot>();
            for (var i = 0; i < gameObject.OGISlots.Count; i++)
            {
                var ogi = gameObject.OGISlots[i];
                ModelSlots.Add(new ModelSlot
                {
                    Ogi = ogi == 65535 ? LabURI.Empty : assetManager.GetUriByTwinId<OGI>(Owner, ogi),
                    Animation = i < gameObject.AnimationSlots.Count ? gameObject.AnimationSlots[i] : ModelSlot.NoAnimation
                });
            }
            BehaviourSlots = new List<LabURI>();
            foreach (var e in gameObject.BehaviourSlots)
            {
                var found = false;
                foreach (var cm in gameObject.RefCodeModels)
                {
                    BehaviourCommandsSequence cmGuid = assetManager.GetAsset<BehaviourCommandsSequence>(package, Owner, cm);
                    if (cmGuid.BehaviourGraphLinks.ContainsKey(e))
                    {
                        BehaviourSlots.Add(cmGuid.BehaviourGraphLinks[e]);
                        found = true;
                        break;
                    }
                }
                // Objects that list nothing still use the sequences of the objects they're copies of
                if (!found && e is > 500 and < 616)
                {
                    var link = assetManager.GetRelatedAssetsOf<BehaviourCommandsSequence>(Owner.Package)
                        .Select(sequence => sequence.BehaviourGraphLinks.GetValueOrDefault(e))
                        .FirstOrDefault(uri => uri != null);
                    if (link != null)
                    {
                        BehaviourSlots.Add(link);
                        found = true;
                    }
                }

                if (!found)
                {
                    var id = e;
                    if (id % 2 == 0)
                    {
                        var allGraphs = assetManager.GetRelatedAssetsOf<BehaviourGraph>(Owner.Package);
                        foreach (var graph in allGraphs)
                        {
                            if (graph.MapStarterIdToSelf(id) == -1)
                            {
                                continue;
                            }
                            
                            id = (ushort)graph.ID;
                            break;
                        }
                    }
                    BehaviourSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUriByTwinId<BehaviourGraph>(Owner, id));
                }
            }
            ObjectSlots = new List<LabURI>();
            foreach (var e in gameObject.ObjectSlots)
            {
                ObjectSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUriByTwinId<GameObject>(Owner, e));
            }
            SoundSlots = new List<LabURI>();
            foreach (var e in gameObject.SoundSlots)
            {
                // A slot of a sound every language has its own of holds one of them, building puts in the others
                var list = CollectMulti5Uri(e);
                SoundSlots.Add(list.Count != 0 ? list[0] : e == 65535 ? LabURI.Empty : assetManager.GetUriByTwinId<SoundEffect>(Owner, e));
            }
            RefObjects = new List<LabURI>();
            foreach (var e in gameObject.RefObjects)
            {
                var uri = assetManager.GetUriByTwinId<GameObject>(Owner, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefObjects.Add(uri);
            }
            RefOGIs = new List<LabURI>();
            foreach (var e in gameObject.RefOGIs)
            {
                var uri = assetManager.GetUriByTwinId<OGI>(Owner, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefOGIs.Add(uri);
            }
            RefAnimations = CloneUtils.CloneList(gameObject.RefAnimations);
            RefBehaviourCommandsSequences = new List<LabURI>();
            foreach (var e in gameObject.RefCodeModels)
            {
                var uri = assetManager.GetUriByTwinId<BehaviourCommandsSequence>(Owner, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefBehaviourCommandsSequences.Add(uri);
            }
            RefBehaviours = new List<LabURI>();
            foreach (var e in gameObject.RefBehaviours)
            {
                // Range reserved for CodeModel(Command sequences) behaviour IDs
                if (e is > 500 and < 616)
                {
                    if (RefBehaviourCommandsSequences.Count > 0)
                    {
                        foreach (var cm in RefBehaviourCommandsSequences)
                        {
                            var cmAsset = assetManager.GetAsset<BehaviourCommandsSequence>(cm);
                            if (cmAsset.BehaviourGraphLinks.ContainsKey(e))
                            {
                                Debug.Assert(cmAsset.BehaviourGraphLinks[e] != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                                RefBehaviours.Add(cmAsset.BehaviourGraphLinks[e]);
                                break;
                            }
                        }
                    }
                    else
                    {
                        var allCms = assetManager.GetRelatedAssetsOf<BehaviourCommandsSequence>(Owner.Package);
                        foreach (var cm in allCms)
                        {
                            if (cm.BehaviourGraphLinks.ContainsKey(e))
                            {
                                Debug.Assert(cm.BehaviourGraphLinks[e] != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                                RefBehaviours.Add(cm.BehaviourGraphLinks[e]);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // We don't care about starters, we'll fix that during export
                    if (e % 2 == 0)
                    {
                        continue;
                    }
                    
                    var uri = assetManager.GetUriByTwinId<BehaviourGraph>(Owner, e);
                    Debug.Assert(uri != LabURI.Empty, $"REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA. ATTEMPTED REFERENCE TO GAME ID {e}");
                    RefBehaviours.Add(uri);
                }
            }
            GraphsWithoutStarter = new List<LabURI>();
            foreach (var e in gameObject.RefBehaviours)
            {
                // Command sequences have their own range of IDs, a starter has the ID before its graph's
                if (e is > 500 and < 616 || e % 2 == 0 || gameObject.RefBehaviours.Contains((UInt16)(e - 1)))
                {
                    continue;
                }

                var uri = assetManager.GetUriByTwinId<BehaviourGraph>(Owner, e);
                if (uri != LabURI.Empty && !GraphsWithoutStarter.Contains(uri))
                {
                    GraphsWithoutStarter.Add(uri);
                }
            }

            RefSounds = new List<LabURI>();
            foreach (var e in gameObject.RefSounds)
            {
                var sndUri = assetManager.GetUriByTwinId<SoundEffect>(Owner, e);
                if (sndUri == LabURI.Empty)
                {
                    var multi5 = CollectMulti5Uri(e);
                    foreach (var snd in multi5)
                    {
                        Debug.Assert(snd != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                        RefSounds.Add(snd);
                    }
                    continue;
                }
                Debug.Assert(sndUri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefSounds.Add(sndUri);
            }
            ReferencesResources = gameObject.ReferencesResources;
            InstanceStateFlags = gameObject.InstanceStateFlags;
            InstFlags = CloneUtils.CloneList(gameObject.InstFlags);
            InstFloats = CloneUtils.CloneList(gameObject.InstFloats);
            InstIntegers = CloneUtils.CloneList(gameObject.InstIntegers);
            BehaviourPack = AgentLabDecompiler.Decompile(gameObject.BehaviourPack);
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((Int32)Type);
            writer.Write(UnkTypeValue);
            writer.Write(CameraReactJointAmount);
            writer.Write(ExitPointAmount);
            writer.Write(Name);

            writer.Write(TriggerBehaviours.Count);
            foreach (var triggerBehaviour in TriggerBehaviours)
            {
                writer.Write((UInt16)(assetManager.GetAsset(triggerBehaviour.TriggerBehaviour).ExportTwinID - 1));
                writer.Write(triggerBehaviour.MessageID);
                writer.Write(triggerBehaviour.BehaviourCallerIndex);
            }

            void writeUriList(IList<LabURI> list)
            {
                writer.Write(list.Count);
                foreach (var item in list)
                {
                    writer.Write((UInt16)(item == LabURI.Empty ? 65535 : assetManager.GetAsset(item).ExportTwinID));
                }
            }
            void writeBehaviourUris(IList<LabURI> uris)
            {
                writer.Write(uris.Count);
                foreach (var uri in uris)
                {
                    if (uri != LabURI.Empty && assetManager.GetAsset(uri) is BehaviourCommandsSequence sequence)
                    {
                        if (!sequence.BehaviourGraphLinks.ContainsValue(uri))
                        {
                            continue;
                        }
                        
                        var neededId = sequence.BehaviourGraphLinks.First(pair => pair.Value == uri).Key;
                        writer.Write((UInt16)neededId);
                    }
                    else
                    {
                        var starterId = -1;
                        if (uri != LabURI.Empty)
                        {
                            var behaviour = assetManager.GetAsset(uri);
                            var compiledGraph = assetManager.GetAssetData<BehaviourGraphData>(uri).GetCompiledBehaviour(factory);
                            if (compiledGraph.Contains<TwinBehaviourStarter>())
                            {
                                starterId = (int)behaviour.ExportTwinID - 1;
                            }
                        }

                        if (starterId == -1)
                        {
                            writer.Write((UInt16)(uri == LabURI.Empty ? 65535 : assetManager.GetAsset(uri).ExportTwinID));
                        }
                        else
                        {
                            writer.Write((UInt16)starterId);
                        }
                    }
                }
            }
            writeUriList(OGISlots.ToList());
            // Animations are written with the IDs they got in the chunk when it got built
            writer.Write(ModelSlots.Count);
            for (var i = 0; i < ModelSlots.Count; i++)
            {
                var slot = ModelSlots[i];
                writer.Write(_animationExportIds != null && i < _animationExportIds.Count ? _animationExportIds[i] : slot.Ogi == LabURI.Empty ? ModelSlot.NoAnimation : slot.Animation);
            }

            writeBehaviourUris(BehaviourSlots);
            writeUriList(ObjectSlots);
            writeUriList(SoundSlots);

            writer.Write((UInt32)InstanceStateFlags);

            void writeParamsList<T>(IList<T> list, Action<T> writeFunc)
            {
                writer.Write(list.Count);
                foreach (var item in list)
                {
                    writeFunc(item);
                }
            }
            writeParamsList(InstFlags, writer.Write);
            writeParamsList(InstFloats, writer.Write);
            writeParamsList(InstIntegers, writer.Write);

            // The game's copies of the startup chunk's objects in levels don't list what they use
            if (!ListsResources(factory))
            {
                for (var i = 0; i < 7; i++)
                {
                    writer.Write(0);
                }
            }
            else
            {
                if (factory.IsDefaultResolution)
                {
                    // Fuck default man for real
                    RefObjects.Insert(0, Owner.URI);
                }
                RefObjects.Add(Owner.URI);
                writeUriList(RefObjects);
                writeUriList(RefOGIs);
                writer.Write(RefAnimations.Count);
                foreach (var animation in RefAnimations)
                {
                    writer.Write(animation);
                }

                writeUriList(RefBehaviourCommandsSequences);
            
                // Cursed behaviour references writing because it must include references to the embedded CodeModel behaviours, behaviour starters and behaviour graphs
                var behaviourRefCount = RefBehaviours.Count;
                foreach (var behaviourUri in RefBehaviours)
                {
                    if (behaviourUri == LabURI.Empty)
                    {
                        continue;
                    }
                
                    var asset = assetManager.GetAsset(behaviourUri);
                    if (asset is BehaviourCommandsSequence)
                    {
                        continue;
                    }
                
                    if (ReferencesStarter(behaviourUri, factory))
                    {
                        behaviourRefCount++;
                    }
                }

                if (factory.IsDefaultResolution && RefBehaviours.Select(b => assetManager.GetAsset(b)).All(a => a is BehaviourCommandsSequence))
                {
                    writer.Write(0);
                }
                else
                {
                    writer.Write(behaviourRefCount);
                    foreach (var uri in RefBehaviours)
                    {
                        if (uri == LabURI.Empty)
                        {
                            writer.Write((UInt16)65535);
                            continue;
                        }

                        var behaviour = assetManager.GetAsset(uri);
                        if (behaviour is BehaviourCommandsSequence sequence)
                        {
                            if (!sequence.BehaviourGraphLinks.ContainsValue(uri))
                            {
                                continue;
                            }

                            var neededId = sequence.BehaviourGraphLinks.First(pair => pair.Value == uri).Key;
                            writer.Write((UInt16)neededId);
                        }
                        else
                        {
                            if (ReferencesStarter(uri, factory))
                            {
                                var starterId = (int)behaviour.ExportTwinID - 1;
                                writer.Write((UInt16)starterId);
                            }

                            writer.Write((UInt16)behaviour.ExportTwinID);
                        }
                    }
                }

                // Write unknowns/unused object refs
                writer.Write(0);

                // The game lists the sounds every language has its own of once
                var soundIds = RefSounds.Where(uri => uri != LabURI.Empty).Select(uri => (UInt16)assetManager.GetAsset(uri).ExportTwinID).Distinct().ToList();
                writer.Write(soundIds.Count);
                foreach (var soundId in soundIds)
                {
                    writer.Write(soundId);
                }
            }

            using var packStream = new MemoryStream();
            using var packWriter = new StreamWriter(packStream);
            packWriter.Write(BehaviourPack);
            packWriter.Flush();
            packStream.Position = 0;
            var commandPack = factory.GenerateBehaviourCommandPack(packStream);
            commandPack.Write(writer);

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateObject(ms);
        }

        // A startup object in a level is a copy of the startup chunk's unless the level had a version of its own
        private Boolean ListsResources(ITwinItemFactory factory)
        {
            var isChunksOwn = factory.ChunkVersions != null && factory.ChunkVersions.TryGetValue((typeof(GameObject), Owner.ID), out var version) && version == Owner.URI;
            return ReferencesResources && (factory.IsDefaultResolution || Owner.Package != factory.GlobalPackage?.URI || isChunksOwn);
        }

        private Boolean ReferencesStarter(LabURI graph, ITwinItemFactory factory)
        {
            return !GraphsWithoutStarter.Contains(graph) && AssetManager.Get().GetAssetData<BehaviourGraphData>(graph).GetCompiledBehaviour(factory).Contains<TwinBehaviourStarter>();
        }

        private static List<LabURI> ObjectsOfBehaviours(IEnumerable<LabURI> behaviours, ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            var objects = new List<LabURI>();
            var visited = new HashSet<LabURI>();
            var pending = new Stack<LabURI>(behaviours);
            while (pending.Count > 0)
            {
                var uri = pending.Pop();
                if (!visited.Add(uri) || !assetManager.DoesAssetExist(uri) || assetManager.GetAsset(uri) is not BehaviourGraph)
                {
                    continue;
                }

                var (referencedObjects, referencedGraphs) = assetManager.GetAssetData<BehaviourGraphData>(uri).GetReferences(factory);
                objects.AddRange(referencedObjects);
                foreach (var graph in referencedGraphs)
                {
                    pending.Push(graph);
                }
            }

            return objects;
        }

        private IEnumerable<LabURI> WithLanguages(LabURI sound)
        {
            var asset = AssetManager.Get().GetAsset(sound);
            return asset is SoundEffectEN or SoundEffectFR or SoundEffectGR or SoundEffectIT or SoundEffectSP or SoundEffectJP
                ? CollectMulti5Uri((UInt16)asset.ID).DefaultIfEmpty(sound)
                : [sound];
        }

        private List<LabURI> CollectMulti5Uri(UInt16 id)
        {
            var result = new List<LabURI>();
            var enUri = AssetManager.Get().GetUriByTwinId<SoundEffectEN>(Owner, id);
            var frUri = AssetManager.Get().GetUriByTwinId<SoundEffectFR>(Owner, id);
            var grUri = AssetManager.Get().GetUriByTwinId<SoundEffectGR>(Owner, id);
            var itUri = AssetManager.Get().GetUriByTwinId<SoundEffectIT>(Owner, id);
            var spUri = AssetManager.Get().GetUriByTwinId<SoundEffectSP>(Owner, id);
            var jpUri = AssetManager.Get().GetUriByTwinId<SoundEffectJP>(Owner, id);

            if (!enUri.Equals(LabURI.Empty))
            {
                result.Add(enUri);
            }
            if (!frUri.Equals(LabURI.Empty))
            {
                result.Add(frUri);
            }
            if (!grUri.Equals(LabURI.Empty))
            {
                result.Add(grUri);
            }
            if (!itUri.Equals(LabURI.Empty))
            {
                result.Add(itUri);
            }
            if (!spUri.Equals(LabURI.Empty))
            {
                result.Add(spUri);
            }
            if (!jpUri.Equals(LabURI.Empty))
            {
                result.Add(jpUri);
            }

            return result;
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
            int? layoutId = null)
        {
            var assetManager = AssetManager.Get();
            var codeSection = section.GetParent();
            var ogiSection = codeSection.GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION);
            var animationSection = codeSection.GetItem<ITwinSection>(Constants.CODE_ANIMATIONS_SECTION);
            var behaviourSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
            var sequenceSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
            
            // The game's copies of the startup chunk's objects in levels don't list what they use, and what the startup chunk's objects use
            // is in the startup chunk unless the level has the object too. Chunks have the models, animations and sounds of every object's
            // slots, but only the behaviours objects list
            if (!ListsResources(factory))
            {
                _animationExportIds = ModelSlots.Select(slot => ResolveAnimation(slot)).ToList();
                foreach (var ogi in OGISlots.Distinct().Where(ogi => ogi != LabURI.Empty))
                {
                    assetManager.GetAsset(ogi).ResolveChunkResources(factory, ogiSection);
                }

                foreach (var sound in SoundSlots.Where(sound => sound != LabURI.Empty).SelectMany(WithLanguages).Distinct())
                {
                    var soundAsset = assetManager.GetAsset(sound);
                    soundAsset.ResolveChunkResources(factory, codeSection.GetItem<ITwinSection>(soundAsset.Section));
                }

                // The objects they spawn are in the chunk, a crate's extra life or health
                var behaviours = BehaviourSlots.Concat(TriggerBehaviours.Select(trigger => trigger.TriggerBehaviour)).Where(uri => uri != LabURI.Empty);
                foreach (var spawned in ObjectSlots.Where(uri => uri != LabURI.Empty).Concat(ObjectsOfBehaviours(behaviours, factory)).Distinct().Where(uri => uri != Owner.URI))
                {
                    assetManager.GetAsset(spawned).ResolveChunkResources(factory, section);
                }

                return base.ResolveChunkResources(factory, section, id, layoutId);
            }

            var refObjects = ObjectSlots.Distinct()
                .Where(b => b != LabURI.Empty).ToList();
            var refOgis = new List<LabURI>();
            var refAnimations = new List<UInt16>();
            var refSounds = new List<LabURI>();
            var refBehaviours = new List<LabURI>();
            var refBehaviourCommandSequences = new List<LabURI>();
            
            var refObjectsStack = new Stack<LabURI>(refObjects);
            while(refObjectsStack.Count > 0)
            {
                var objAsset = assetManager.GetAsset<GameObject>(refObjectsStack.Pop());
                objAsset.ResolveChunkResources(factory, section);
                // Objects being resolved further up refer back to this one, theirs aren't known yet
                if (factory.Resolution.ObjectReferences.TryGetValue(objAsset.URI, out var references))
                {
                    foreach (var refObject in references.RefObjects)
                    {
                        if (refObjects.Contains(refObject) || refObject == Owner.URI)
                        {
                            continue;
                        }
                        
                        refObjects.Add(refObject);
                        refObjectsStack.Push(refObject);
                    }
                    
                    foreach (var refBehaviour in references.RefBehaviours)
                    {
                        if (refBehaviours.Contains(refBehaviour))
                        {
                            continue;
                        }
                        refBehaviours.Add(refBehaviour);
                    }
                    
                    foreach (var refAnimation in references.RefAnimations)
                    {
                        if (refAnimations.Contains(refAnimation))
                        {
                            continue;
                        }
                        refAnimations.Add(refAnimation);
                    }
                    
                    foreach (var refOgi in references.RefOgis)
                    {
                        if (refOgis.Contains(refOgi))
                        {
                            continue;
                        }
                        refOgis.Add(refOgi);
                    }
                    
                    foreach (var refSound in references.RefSounds)
                    {
                        if (refSounds.Contains(refSound))
                        {
                            continue;
                        }
                        refSounds.Add(refSound);
                    }
                }
            }
            
            refOgis.AddRange(OGISlots.Distinct()
                .Where(b => b != LabURI.Empty)
                .Where(b => !refOgis.Contains(b)).ToList());
            _animationExportIds = ModelSlots.Select(slot => ResolveAnimation(slot)).ToList();
            refAnimations.AddRange(_animationExportIds.Distinct()
                .Where(b => b != ModelSlot.NoAnimation)
                .Where(b => !refAnimations.Contains(b)).ToList());
            refSounds.AddRange(SoundSlots.Distinct()
                .Where(b => b != LabURI.Empty)
                .Where(b => !refSounds.Contains(b)).ToList());
            refBehaviours.AddRange(BehaviourSlots.Distinct()
                .Where(b => b != LabURI.Empty)
                .Where(b => !refBehaviours.Contains(b)).ToList());
            refBehaviours.AddRange(TriggerBehaviours
                .Select(objectTriggerBehaviourData => objectTriggerBehaviourData.TriggerBehaviour)
                .Where(b => !refBehaviours.Contains(b)));

            var behaviourReferenceStack = new Stack<LabURI>(refBehaviours);
            while (behaviourReferenceStack.Count > 0)
            {
                var behaviour = assetManager.GetAsset(behaviourReferenceStack.Pop());
                if (behaviour is BehaviourCommandsSequence)
                {
                    if (!refBehaviourCommandSequences.Contains(behaviour.URI))
                    {
                        refBehaviourCommandSequences.Add(behaviour.URI);
                    }

                    continue;
                }

                behaviour.ResolveChunkResources(factory, behaviourSection);
                if (!factory.Resolution.GraphReferences.TryGetValue(behaviour.URI, out var graphReferences))
                {
                    continue;
                }

                refObjects.AddRange(graphReferences.Objects);
                refObjects = refObjects.Distinct().ToList();
                refBehaviours.AddRange(graphReferences.Graphs);
                refBehaviours = refBehaviours.Distinct().ToList();
                foreach (var uri in graphReferences.Graphs.Distinct().Where(uri => !behaviourReferenceStack.Contains(uri)))
                {
                    behaviourReferenceStack.Push(uri);
                }
            }

            foreach (var sequence in refBehaviourCommandSequences)
            {
                var seqAss = assetManager.GetAsset(sequence);
                if (seqAss.ExportTwinID == 0 && !factory.IsDefaultResolution)
                {
                    continue;
                }
                
                seqAss.ResolveChunkResources(factory, sequenceSection);
            }
            
            foreach (var ogi in refOgis)
            {
                assetManager.GetAsset(ogi).ResolveChunkResources(factory, ogiSection);
            }

            foreach (var sfx in refSounds.SelectMany(WithLanguages).Distinct())
            {
                var sfxAsset = assetManager.GetAsset(sfx);
                var sfxSection = codeSection.GetItem<ITwinSection>(sfxAsset.Section);
                sfxAsset.ResolveChunkResources(factory, sfxSection);
            }
            
            var resultingBehaviourRefs = new List<LabURI>();
            var addedSeqGraphLinks = new HashSet<uint>();
            foreach (var refBehaviour in refBehaviours)
            {
                var behaviour = assetManager.GetAsset(refBehaviour);
                if (behaviour is not BehaviourCommandsSequence seqAss)
                {
                    resultingBehaviourRefs.Add(refBehaviour);
                    continue;
                }

                foreach (var seqAssBehaviourGraphLink in seqAss.BehaviourGraphLinks)
                {
                    if (addedSeqGraphLinks.Contains(seqAssBehaviourGraphLink.Key) || seqAssBehaviourGraphLink.Value != refBehaviour)
                    {
                        continue;
                    }
                    
                    resultingBehaviourRefs.Add(seqAssBehaviourGraphLink.Value);
                    addedSeqGraphLinks.Add(seqAssBehaviourGraphLink.Key);
                }
            }

            foreach (var graph in resultingBehaviourRefs.Where(uri => assetManager.GetAsset(uri) is BehaviourGraph))
            {
                if (ReferencesStarter(graph, factory))
                {
                    assetManager.GetAssetData<BehaviourGraphData>(graph).AddStarter(factory, behaviourSection, assetManager.GetAsset(graph).ExportTwinID);
                }
            }

            RefObjects = refObjects;
            RefBehaviours = resultingBehaviourRefs;
            RefAnimations = refAnimations;
            RefSounds = refSounds;
            RefBehaviourCommandsSequences = refBehaviourCommandSequences;
            RefOGIs = refOgis;
            
            // Copies, the lists are the data's and disposing it clears them
            factory.Resolution.ObjectReferences[Owner.URI] = new GameObject.ReferencedResourceUris
            {
                RefBehaviours = resultingBehaviourRefs.ToList(),
                RefAnimations = refAnimations.ToList(),
                RefOgis = refOgis.ToList(),
                RefSounds = refSounds.ToList(),
                RefObjects = refObjects.ToList()
            };

            return base.ResolveChunkResources(factory, section, id, layoutId);

            // The animation is the OGI's, it goes into the chunk with the ID it has there
            UInt16 ResolveAnimation(ModelSlot slot)
            {
                if (slot.Ogi == LabURI.Empty || slot.Animation == ModelSlot.NoAnimation || !assetManager.DoesAssetExist(slot.Ogi))
                {
                    return ModelSlot.NoAnimation;
                }

                var exportId = assetManager.GetAssetData<OGIData>(slot.Ogi).ResolveAnimation(factory, animationSection, slot.Animation);
                return exportId is < ModelSlot.NoAnimation ? (UInt16)exportId.Value : ModelSlot.NoAnimation;
            }
        }
    }

}
