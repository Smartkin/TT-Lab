using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Instance
{
    [ReferencesAssets]
    public class CollisionSurfaceData : AbstractAssetData
    {
        public CollisionSurfaceData()
        {
            SurfaceID = SurfaceType.SURF_DEFAULT;
            PhysicsParameters = new float[10];
            UnkVec = new Vector4();
            UnkBoundingBox = new Vector4[] { new(0, 0, 0, 1), new(10, 10, 10, 1) };
            StepSoundId1 = LabURI.Empty;
            StepSoundId2 = LabURI.Empty;
            LandSoundId1 = LabURI.Empty;
            LandSoundId2 = LabURI.Empty;
            UnkSoundId = LabURI.Empty;
        }

        public CollisionSurfaceData(ITwinSurface collisionSurface) : this()
        {
            SetTwinItem(collisionSurface);
        }

        [JsonProperty(Required = Required.Always)]
        public SurfaceType SurfaceID { get; set; }
        [JsonProperty(Required = Required.Always)]
        public SurfaceFlags Flags { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI StepSoundId1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI StepSoundId2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 WalkOnParticleSystemId1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 WalkOnParticleSystemId2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI LandSoundId1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 UnkId3 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 LandOnParticleSystemId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI LandSoundId2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI UnkSoundId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Single[] PhysicsParameters { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4 UnkVec { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Vector4[] UnkBoundingBox { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            return;
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinSurface collisionSurface = GetTwinItem<ITwinSurface>();
            SurfaceID = collisionSurface.SurfaceId;
            Flags = collisionSurface.Flags;
            StepSoundId1 = LabURI.Empty;
            StepSoundId2 = LabURI.Empty;
            LandSoundId1 = LabURI.Empty;
            LandSoundId2 = LabURI.Empty;
            UnkSoundId = LabURI.Empty;
            if (collisionSurface.StepSoundId1 != 0xFFFF)
            {
                StepSoundId1 = AssetManager.Get().GetUri(package, nameof(SoundEffect), variant, collisionSurface.StepSoundId1);
            }
            if (collisionSurface.StepSoundId2 != 0xFFFF)
            {
                StepSoundId2 = AssetManager.Get().GetUri(package, nameof(SoundEffect), variant, collisionSurface.StepSoundId2);
            }
            if (collisionSurface.LandSoundId1 != 0xFFFF)
            {
                LandSoundId1 = AssetManager.Get().GetUri(package, nameof(SoundEffect), variant, collisionSurface.LandSoundId1);
            }
            if (collisionSurface.LandSoundId2 != 0xFFFF)
            {
                LandSoundId2 = AssetManager.Get().GetUri(package, nameof(SoundEffect), variant, collisionSurface.LandSoundId2);
            }
            if (collisionSurface.UnkSoundId != 0xFFFF)
            {
                UnkSoundId = AssetManager.Get().GetUri(package, nameof(SoundEffect), variant, collisionSurface.UnkSoundId);
            }
            WalkOnParticleSystemId1 = collisionSurface.WalkOnParticleSystemId;
            WalkOnParticleSystemId2 = collisionSurface.WalkOnParticleSystemId2;
            UnkId3 = collisionSurface.UnkId3;
            LandOnParticleSystemId = collisionSurface.LandOnParticleSystemId;
            PhysicsParameters = CloneUtils.CloneArray(collisionSurface.PhysicsParameters);
            UnkVec = CloneUtils.Clone(collisionSurface.UnkVec);
            UnkBoundingBox = new Vector4[2];
            for (var i = 0; i < UnkBoundingBox.Length; ++i)
            {
                UnkBoundingBox[i] = CloneUtils.Clone(collisionSurface.UnkBoundingBox[i]);
            }
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((UInt32)Flags);
            writer.Write((UInt16)SurfaceID);
            writer.Write(StepSoundId1 == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)assetManager.GetAsset(StepSoundId1).ID);
            writer.Write(StepSoundId2 == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)assetManager.GetAsset(StepSoundId2).ID);
            writer.Write(WalkOnParticleSystemId1);
            writer.Write(WalkOnParticleSystemId2);
            writer.Write(LandSoundId1 == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)assetManager.GetAsset(LandSoundId1).ID);
            writer.Write(UnkId3);
            writer.Write(LandOnParticleSystemId);
            writer.Write(LandSoundId2 == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)assetManager.GetAsset(LandSoundId2).ID);
            writer.Write(UnkSoundId == LabURI.Empty ? (UInt16)0xFFFF : (UInt16)assetManager.GetAsset(UnkSoundId).ID);
            writer.Write((UInt16)0xFFFF); // Unused ID
            foreach (var param in PhysicsParameters)
            {
                writer.Write(param);
            }
            UnkVec.Write(writer);
            foreach (var vec in UnkBoundingBox)
            {
                vec.Write(writer);
            }

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateSurface(ms);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var soundSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_SOUND_EFFECTS_SECTION);

            if (StepSoundId1 != LabURI.Empty)
            {
                assetManager.GetAsset(StepSoundId1).ResolveChunkResources(factory, soundSection);
            }

            if (StepSoundId2 != LabURI.Empty)
            {
                assetManager.GetAsset(StepSoundId2).ResolveChunkResources(factory, soundSection);
            }

            if (LandSoundId1 != LabURI.Empty)
            {
                assetManager.GetAsset(LandSoundId1).ResolveChunkResources(factory, soundSection);
            }

            if (LandSoundId2 != LabURI.Empty)
            {
                assetManager.GetAsset(LandSoundId2).ResolveChunkResources(factory, soundSection);
            }

            if (UnkSoundId != LabURI.Empty)
            {
                assetManager.GetAsset(UnkSoundId).ResolveChunkResources(factory, soundSection);
            }

            return base.ResolveChunkResources(factory, section, id);
        }
    }
}
