using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Graphics.SubModels;
using Twinsanity.AgentLab;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Assets.Factory
{
    public class XboxItemFactory : ITwinItemFactory
    {
        public ITwinAIPath GenerateAIPath(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinAIPosition GenerateAIPosition(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinAnimation GenerateAnimation(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinBehaviourCommandsSequence GenerateBehaviourCommandsSequence(Stream stream)
        {
            throw new NotImplementedException();
        }

        public AgentLabCompiler.CompilerResult GenerateBehaviourGraph(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinBehaviourCommandPack GenerateBehaviourCommandPack(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<SubBlendData> blends, UInt32? compileScale)
        {
            throw new NotImplementedException();
        }

        public ITwinCamera GenerateCamera(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinCollision GenerateCollision(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinDynamicScenery GenerateDynamicScenery(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinInstance GenerateInstance(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinLink GenerateLink(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinLOD GenerateLOD(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinMaterial GenerateMaterial(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinMesh GenerateMesh(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinModel GenerateModel(List<MeshProcessor.Mesh> meshes)
        {
            throw new NotImplementedException();
        }

        public ITwinObject GenerateObject(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinOGI GenerateOGI(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinParticle GenerateParticle(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinDefaultParticle GenerateDefaultParticle(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinPath GeneratePath(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinPosition GeneratePosition(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinRigidModel GenerateRigidModel(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinScenery GenerateScenery(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinSkin GenerateSkin(List<SubSkinData> subskins)
        {
            throw new NotImplementedException();
        }

        public ITwinSkydome GenerateSkydome(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinSound GenerateSound()
        {
            return new XboxAnySound();
        }

        public ITwinSurface GenerateSurface(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinTemplate GenerateTemplate(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinTexture GenerateTexture()
        {
            return new XboxAnyTexture();
        }

        public ITwinTrigger GenerateTrigger(Stream stream)
        {
            throw new NotImplementedException();
        }

        public ITwinSection GenerateFrontend(List<ITwinSound> sounds)
        {
            throw new NotImplementedException();
        }

        public ITwinPSF GenerateFont(List<ITwinPTC> pages, List<Vector4> unkVecs, Int32 unkInt)
        {
            throw new NotImplementedException();
        }

        public ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material)
        {
            throw new NotImplementedException();
        }

        public ITwinPSM GeneratePSM(List<ITwinPTC> ptcs)
        {
            throw new NotImplementedException();
        }

        public ITwinSection GenerateDefault()
        {
            throw new NotImplementedException();
        }

        public ITwinSection GenerateRM()
        {
            throw new NotImplementedException();
        }

        public ITwinSection GenerateSM()
        {
            throw new NotImplementedException();
        }
    }
}
