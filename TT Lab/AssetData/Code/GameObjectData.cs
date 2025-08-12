using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
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
        public GameObjectData()
        {
            Name = "NewGameObject";
            Type = ITwinObject.ObjectType.GenericObject;
            UnkTypeValue = 1;
            TriggerBehaviours = new List<ObjectTriggerBehaviourData>();
            OGISlots = new List<LabURI>();
            AnimationSlots = new List<LabURI>();
            BehaviourSlots = new List<LabURI>();
            ObjectSlots = new List<LabURI>();
            SoundSlots = new List<LabURI>();
            InstFlags = new List<UInt32>();
            InstFloats = new List<float>();
            InstIntegers = new List<UInt32>();
            RefObjects = new List<LabURI>();
            RefBehaviours = new List<LabURI>();
            RefSounds = new List<LabURI>();
            RefAnimations = new List<LabURI>();
            RefOGIs = new List<LabURI>();
            RefBehaviourCommandsSequences = new List<LabURI>();
            BehaviourPack = string.Empty;
        }

        public GameObjectData(ITwinObject gameObject, Dictionary<string, TwinBehaviourStarter> starterMap)
        {
            _starterMap = starterMap;
            SetTwinItem(gameObject);
        }

        public GameObjectData(String path) => Load(path, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        });

        [JsonProperty(Required = Required.Always)]
        public ITwinObject.ObjectType Type { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte UnkTypeValue { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte CameraReactJointAmount { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte ExitPointAmount { get; set; }
        [JsonProperty(Required = Required.Always)]
        public String Name { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<ObjectTriggerBehaviourData> TriggerBehaviours { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> OGISlots { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> AnimationSlots { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> BehaviourSlots { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> ObjectSlots { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> SoundSlots { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Enums.InstanceState InstanceStateFlags { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<UInt32> InstFlags { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Single> InstFloats { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<UInt32> InstIntegers { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefObjects { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefOGIs { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefAnimations { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefBehaviourCommandsSequences { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefBehaviours { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RefSounds { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string BehaviourPack { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            TriggerBehaviours.Clear();
            OGISlots.Clear();
            AnimationSlots.Clear();
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
        }

        private Dictionary<string, TwinBehaviourStarter> _starterMap;
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
                TriggerBehaviours.Add(new ObjectTriggerBehaviourData(package, variant, e, _starterMap));
            }
            OGISlots = new List<LabURI>();
            foreach (var e in gameObject.OGISlots)
            {
                OGISlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUri(package, nameof(OGI), variant, e));
            }
            AnimationSlots = new List<LabURI>();
            foreach (var e in gameObject.AnimationSlots)
            {
                AnimationSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUri(package, nameof(Animation), variant, e));
            }
            BehaviourSlots = new List<LabURI>();
            foreach (var e in gameObject.BehaviourSlots)
            {
                var found = false;
                foreach (var cm in gameObject.RefCodeModels)
                {
                    BehaviourCommandsSequence cmGuid = assetManager.GetAsset<BehaviourCommandsSequence>(package, nameof(BehaviourCommandsSequence), variant, cm);
                    if (cmGuid.BehaviourGraphLinks.ContainsKey(e))
                    {
                        BehaviourSlots.Add(cmGuid.BehaviourGraphLinks[e]);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var id = e;
                    if (id % 2 == 0)
                    {
                        var allGraphs = assetManager.GetAllAssetsOf<BehaviourGraph>().Cast<BehaviourGraph>();
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
                    BehaviourSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUri(package, nameof(BehaviourGraph), variant, id));
                }
            }
            ObjectSlots = new List<LabURI>();
            foreach (var e in gameObject.ObjectSlots)
            {
                ObjectSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUri(package, nameof(GameObject), variant, e));
            }
            SoundSlots = new List<LabURI>();
            foreach (var e in gameObject.SoundSlots)
            {
                var list = CollectMulti5Uri(package, null, e);
                if (list.Count != 0)
                {
                    SoundSlots.AddRange(list);
                }
                else
                {
                    SoundSlots.Add((e == 65535) ? LabURI.Empty : assetManager.GetUri(package, nameof(SoundEffect), variant, e));
                }
            }
            RefObjects = new List<LabURI>();
            foreach (var e in gameObject.RefObjects)
            {
                var uri = assetManager.GetUri(package, nameof(GameObject), variant, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefObjects.Add(uri);
            }
            RefOGIs = new List<LabURI>();
            foreach (var e in gameObject.RefOGIs)
            {
                var uri = assetManager.GetUri(package, nameof(OGI), variant, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefOGIs.Add(uri);
            }
            RefAnimations = new List<LabURI>();
            foreach (var e in gameObject.RefAnimations)
            {
                var uri = assetManager.GetUri(package, nameof(Animation), variant, e);
                Debug.Assert(uri != LabURI.Empty, "REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA");
                RefAnimations.Add(uri);
            }
            RefBehaviourCommandsSequences = new List<LabURI>();
            foreach (var e in gameObject.RefCodeModels)
            {
                var uri = assetManager.GetUri(package, nameof(BehaviourCommandsSequence), variant, e);
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
                        var allCms = assetManager.GetAllAssetsOf<BehaviourCommandsSequence>().Cast<BehaviourCommandsSequence>();
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
                    
                    var uri = assetManager.GetUri(package, nameof(BehaviourGraph), variant, e);
                    Debug.Assert(uri != LabURI.Empty, $"REFERENCES CAN NOT CONTAIN REFERENCE TO NULL DATA. ATTEMPTED REFERENCE TO GAME ID {e}");
                    RefBehaviours.Add(uri);
                }
            }
            RefSounds = new List<LabURI>();
            foreach (var e in gameObject.RefSounds)
            {
                var sndUri = assetManager.GetUri(package, nameof(SoundEffect), variant, e);
                if (sndUri == LabURI.Empty)
                {
                    var multi5 = CollectMulti5Uri(package, null, e);
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
                writer.Write((UInt16)(assetManager.GetAsset(triggerBehaviour.TriggerBehaviour).ID - 1));
                writer.Write(triggerBehaviour.MessageID);
                writer.Write(triggerBehaviour.BehaviourCallerIndex);
            }

            void writeUriList(IList<LabURI> list)
            {
                writer.Write(list.Count);
                foreach (var item in list)
                {
                    writer.Write((UInt16)(item == LabURI.Empty ? 65535 : assetManager.GetAsset(item).ID));
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
                                starterId = (int)behaviour.ID - 1;
                            }
                        }

                        if (starterId == -1)
                        {
                            writer.Write((UInt16)(uri == LabURI.Empty ? 65535 : assetManager.GetAsset(uri).ID));
                        }
                        else
                        {
                            writer.Write((UInt16)starterId);
                        }
                    }
                }
            }
            writeUriList(OGISlots);
            writeUriList(AnimationSlots);
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

            writeUriList(RefObjects);
            writeUriList(RefOGIs);
            writeUriList(RefAnimations);
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
                
                var compiledGraph = assetManager.GetAssetData<BehaviourGraphData>(behaviourUri).GetCompiledBehaviour(factory);
                if (compiledGraph.Contains<TwinBehaviourStarter>())
                {
                    behaviourRefCount++;
                }
            }
            writer.Write(behaviourRefCount);
            foreach (var uri in RefBehaviours)
            {
                var behaviour = assetManager.GetAsset(uri);
                if (uri != LabURI.Empty && behaviour is BehaviourCommandsSequence sequence)
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
                    if (uri != LabURI.Empty)
                    {
                        var compiledGraph = assetManager.GetAssetData<BehaviourGraphData>(uri).GetCompiledBehaviour(factory);
                        if (compiledGraph.Contains<TwinBehaviourStarter>())
                        {
                            var starterId = (int)behaviour.ID - 1;
                            writer.Write((UInt16)starterId);
                        }
                        writer.Write((UInt16)behaviour.ID);
                    }
                    else
                    {
                        writer.Write((UInt16)(uri == LabURI.Empty ? 65535 : assetManager.GetAsset(uri).ID));
                    }
                }
            }

            // Write unknowns/unused object refs
            writer.Write(0);

            writeUriList(RefSounds);

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

        private static List<LabURI> CollectMulti5Uri(LabURI package, String? variant, UInt16 id)
        {
            var result = new List<LabURI>();
            var enUri = AssetManager.Get().GetUri(package, nameof(SoundEffectEN), variant, id);
            var frUri = AssetManager.Get().GetUri(package, nameof(SoundEffectFR), variant, id);
            var grUri = AssetManager.Get().GetUri(package, nameof(SoundEffectGR), variant, id);
            var itUri = AssetManager.Get().GetUri(package, nameof(SoundEffectIT), variant, id);
            var spUri = AssetManager.Get().GetUri(package, nameof(SoundEffectSP), variant, id);
            var jpUri = AssetManager.Get().GetUri(package, nameof(SoundEffectJP), variant, id);

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

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var codeSection = section.GetParent();
            var ogiSection = codeSection.GetItem<ITwinSection>(Constants.CODE_OGIS_SECTION);
            var animationSection = codeSection.GetItem<ITwinSection>(Constants.CODE_ANIMATIONS_SECTION);
            var behaviourSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
            var sequenceSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);

            foreach (var @object in RefObjects)
            {
                assetManager.GetAsset(@object).ResolveChunkResources(factory, section);
            }

            foreach (var animation in RefAnimations)
            {
                assetManager.GetAsset(animation).ResolveChunkResources(factory, animationSection);
            }

            foreach (var behaviour in RefBehaviours.Where(behaviour => assetManager.GetAsset(behaviour) is not BehaviourCommandsSequence))
            {
                assetManager.GetAsset(behaviour).ResolveChunkResources(factory, behaviourSection);
            }

            foreach (var sequence in RefBehaviourCommandsSequences)
            {
                assetManager.GetAsset(sequence).ResolveChunkResources(factory, sequenceSection);
            }

            foreach (var ogi in RefOGIs)
            {
                assetManager.GetAsset(ogi).ResolveChunkResources(factory, ogiSection);
            }

            foreach (var sfx in RefSounds)
            {
                var sfxAsset = assetManager.GetAsset(sfx);
                var sfxSection = codeSection.GetItem<ITwinSection>(sfxAsset.Section);
                sfxAsset.ResolveChunkResources(factory, sfxSection);
            }

            return base.ResolveChunkResources(factory, section, id, layoutID);
        }
    }

}
