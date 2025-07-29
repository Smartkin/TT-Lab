using System;
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
        ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<SubBlendData> blends, UInt32? compileScale);
        ITwinLOD GenerateLOD(Stream stream);
        ITwinMaterial GenerateMaterial(Stream stream);
        ITwinMesh GenerateMesh(Stream stream);
        ITwinModel GenerateModel(List<MeshProcessor.Mesh> meshes);
        ITwinRigidModel GenerateRigidModel(Stream stream);
        ITwinSkin GenerateSkin(List<SubSkinData> subskins);
        ITwinSkydome GenerateSkydome(Stream stream);
        ITwinTexture GenerateTexture();
        ITwinAnimation GenerateAnimation(Stream stream);
        AgentLabCompiler.CompilerResult GenerateBehaviourGraph(Stream stream);
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
        ITwinPSF GenerateFont(List<ITwinPTC> pages, List<Vector4> unkVecs, Int32 unkInt);
        ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material);
        ITwinPSM GeneratePSM(List<ITwinPTC> ptcs);
        ITwinSection GenerateDefault();
        ITwinSection GenerateRM();
        ITwinSection GenerateSM();
    }
}
