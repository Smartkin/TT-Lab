using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Code.Resolvers.Decompiler;
using TT_Lab.Assets.Factory;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetData.Code.Behaviour
{
    public class BehaviourGraphData : AbstractAssetData
    {
        private int _graphId = -1;
        private TwinBehaviourStarter? _starter;
        
        private static string _behaviourTemplate = """
                                                   [StartFrom(StartingState)] // This attribute is optional. You can remove it and the first declared State will be used as the starting one
                                                   [Priority(20)] // Behaviour's priority. Behaviours with higher priority will take over if they're triggered while another behaviour is running. This attribute is optional and the priority will default to 0
                                                   behaviour COM_RENAME_ME { // Behaviour's name can be anything
                                                        
                                                        // This allows for the behaviour to be executed when an object receives some sort of event (gets damaged, created, etc.)
                                                        // otherwise the behaviour can only be executed/referenced from other scripts.
                                                        // The provided defaults here indicate that the object itself will always initiate the behaviour during any situation.
                                                        // Multiple assigners can be provided 
                                                        starter {
                                                            assigner = {
                                                                AssignType = ME;
                                                                AssignLocality = ANYWHERE;
                                                                AssignStatus = ANYSTATE;
                                                                AssignPreference = ANYHOW;
                                                            }
                                                        }
                                                   
                                                        state StartingState() { // State name can be anything for clarity's sake
                                                            if Else(0) >= 1 {
                                                                interval = 0; // Any interval value <= 0 will check the condition every game frame. Mind this for performance
                                                                
                                                                // After this provide a list of actions in sequence to execute when the condition is met. They will be executed top to bottom
                                                                MakeInert();
                                                                
                                                                execute NextState; // This is optional and you can stay executing the current state if you wish and do a jump in a different condition
                                                            }
                                                        }
                                                        
                                                        // Notable attributes for states:
                                                        // [SkipsFirstBody] - will skip the first condition check
                                                        // [NonBlocking] - It is still not quite known what this actually enables but in theory this will allow other states to check for conditions in parallel to the state that has this attribute
                                                        // [UseObjectSlot(SLOT_NAME)] - The state will execute a script that is referenced by an object's event (created, damaged, etc.)
                                                        // [ControlPacket(CONTROL_PACKET_NAME)] - The state will execute a continuous movement (rotating, translating, etc.) depending on how you setup the referenced control packet 
                                                        
                                                        // States can be empty and what is assumingly signifies that the behaviour finishes execution
                                                        state NextState() {
                                                        }
                                                        
                                                        // For other stuff like ControlPackets and other attribute usage look at game's original scripts
                                                   }
                                                   """;
        
        public BehaviourGraphData()
        {
            Graph = _behaviourTemplate[..];
        }

        public BehaviourGraphData(ITwinBehaviourGraph mainScript, TwinBehaviourStarter? starter = null) : this()
        {
            SetTwinItem(mainScript);
            SetStarter(starter);
        }

        public String Graph { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            Graph = "";
        }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            using var fs = new FileStream(dataPath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(fs);
            writer.Write(Graph.ToCharArray());
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(fs);
            Graph = reader.ReadToEnd();
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var graph = GetTwinItem<ITwinBehaviourGraph>();
            var starter = _starter;
            IStarterAssignerGlobalObjectIdResolversList? globalObjectIdResolver = null;
            if (starter != null)
            {
                globalObjectIdResolver = new DefaultStarterAssignerGlobalObjectIdResolversList(starter.Assigners.Select(assigner => new LabStarterAssignerGlobalObjectIdResolver(package, variant, assigner.GlobalObjectId)).Cast<IStarterAssignerGlobalObjectIdResolver>().ToArray());
            }
            
            var stateList = new List<IStateResolver>();
            foreach (var state in graph.ScriptStates)
            {
                string? graphName = null;
                if (state.BehaviourIndexOrSlot != -1 && !state.UsesObjectSlot)
                {
                    graphName = AssetManager.Get().GetUri(package, nameof(BehaviourGraph), variant, (uint)state.BehaviourIndexOrSlot);
                }

                stateList.Add(new DefaultStateResolver(graphName));
            }
            var stateResolver = new DefaultStateResolversList(stateList.ToArray());
            var resolver = new DefaultGraphResolver(new DefaultStarterResolver(starter, globalObjectIdResolver), stateResolver);
            Graph = AgentLabDecompiler.Decompile(graph, resolver);
        }

        public AgentLabCompiler.CompilerResult GetCompiledBehaviour(ITwinItemFactory factory)
        {
            using var ms = new MemoryStream();
            using var binaryWriter = new BinaryWriter(ms);
            binaryWriter.Write(_graphId);
            using var writer = new StreamWriter(ms);
            writer.Write(Graph);
            writer.Flush();

            ms.Position = 0;
            return factory.GenerateBehaviourGraph(ms);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id, int? layoutID = null)
        {
            _graphId = (int)id;
            var compiledBehaviour = GetCompiledBehaviour(factory);

            if (compiledBehaviour.Contains<TwinBehaviourStarter>())
            {
                var starterId = id - 1;
                var starter = compiledBehaviour.Get<TwinBehaviourStarter>();
                starter.SetID(starterId);
                if (!section.ContainsItem(starterId))
                {
                    section.AddItem(starter);
                }
            }
            
            if (section.ContainsItem(id))
            {
                return null;
            }

            var item = compiledBehaviour.Get<ITwinBehaviourGraph>();
            item.SetID(id);
            section.AddItem(item);
            return item;
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            return GetCompiledBehaviour(factory).Get<ITwinBehaviourGraph>();
        }

        private void SetStarter(TwinBehaviourStarter? starter)
        {
            _starter = starter;
        }
    }
}
