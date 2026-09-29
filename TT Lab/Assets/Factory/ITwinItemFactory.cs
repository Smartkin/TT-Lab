using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Graphics.SubModels;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Assets.Factory
{
    public interface ITwinItemFactory
    {
        public Package GlobalPackage { get; set; }
        public bool IsDefaultResolution { get; set; }
        public string ChunkPath { get; set; }
        /// <summary>
        /// The versions of the items that differ between chunks the chunk being built has, by their type and ID
        /// </summary>
        public IReadOnlyDictionary<(Type, UInt32), LabURI>? ChunkVersions { get; set; }
        /// <summary>
        /// The chunk being built's own values of the assets it shares with other chunks
        /// </summary>
        public ChunkOverrides? Overrides { get; set; }
        /// <summary>
        /// What the chunk being built has resolved so far
        /// </summary>
        public ChunkResolution Resolution { get; }
        /// <summary>
        /// Behaviours compiled by this build, shared by the factories of its chunks
        /// </summary>
        public ConcurrentDictionary<(LabURI Graph, Int32 Id, String Script), AgentLabCompiler.CompilerResult> CompiledBehaviours { get; }
        /// <summary>
        /// The chunks the build's profile leaves out, links to them are dropped from the chunks being written
        /// </summary>
        public IReadOnlySet<LabURI> ExcludedChunks { get; set; }
        /// <summary>
        /// The chunks the chunk being built links to, dropped ones included, for the build cache
        /// </summary>
        public List<LabURI> LinkedChunks { get; }

        /// <summary>
        /// A factory of the same build for a chunk, chunks build in parallel with a factory each
        /// </summary>
        ITwinItemFactory ForChunk();

        ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<BlendPartExport> parts);
        ITwinLOD GenerateLOD(Stream stream);
        ITwinMaterial GenerateMaterial(Stream stream);
        ITwinMesh GenerateMesh(Stream stream);
        ITwinModel GenerateModel(List<RigidPartExport> parts);
        ITwinRigidModel GenerateRigidModel(Stream stream);
        ITwinSkin GenerateSkin(List<SkinPartExport> parts);
        ITwinSkydome GenerateSkydome(Stream stream);
        ITwinTexture GenerateTexture();
        ITwinAnimation GenerateAnimation(Stream stream);
        /// <summary>
        /// Compiles a graph's script (its ID, then the text). The requester's package tells which behaviours the script's states can name
        /// </summary>
        AgentLabCompiler.CompilerResult GenerateBehaviourGraph(Stream stream, IAsset? requester = null);
        ITwinBehaviourCommandsSequence GenerateBehaviourCommandsSequence(Stream stream);
        ITwinBehaviourCommandPack GenerateBehaviourCommandPack(Stream stream);
        ITwinObject GenerateObject(Stream stream);
        ITwinOGI GenerateOGI(Stream stream);
        ITwinSound GenerateSound();
        ITwinAIPath GenerateAIPath(Stream stream);
        ITwinAIPosition GenerateAIPosition(Stream stream);
        ITwinCamera GenerateCamera(Stream stream);
        ITwinInstance GenerateInstance(Stream stream);
        ITwinPath GeneratePath(Stream stream);
        ITwinPosition GeneratePosition(Stream stream);
        ITwinSurface GenerateSurface(Stream stream);
        ITwinTemplate GenerateTemplate(Stream stream);
        ITwinTrigger GenerateTrigger(Stream stream);
        ITwinCollision GenerateCollision(Stream stream);
        ITwinParticle GenerateParticle(Stream stream);
        ITwinDefaultParticle GenerateDefaultParticle(Stream stream);
        ITwinDynamicScenery GenerateDynamicScenery(Stream stream);
        ITwinLink GenerateLink(Stream stream);
        ITwinScenery GenerateScenery(Stream stream);
        ITwinSection GenerateFrontend(List<ITwinSound> sounds);
        ITwinPSF GenerateFont(List<ITwinPTC> pages, List<VectorCharacterData> characterData, Int32 spaceIdentifier);
        ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material);
        ITwinPSM GeneratePSM(List<ITwinPTC> ptcs);
        ITwinSection GenerateDefault();
        ITwinSection GenerateRM();
        ITwinSection GenerateSM();
    }
}
