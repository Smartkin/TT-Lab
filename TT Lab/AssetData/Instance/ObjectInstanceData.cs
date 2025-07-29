using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.AssetData.Instance
{
    [ReferencesAssets]
    public class ObjectInstanceData : AbstractAssetData
    {
        public ObjectInstanceData()
        {
            InstancesRelated = 10;
            PathsRelated = 10;
            PositionsRelated = 10;
            Position = new Vector4(0, 0, 0, 1);
            RotationX = new TwinIntegerRotation();
            RotationY = new TwinIntegerRotation();
            RotationZ = new TwinIntegerRotation();
            Instances = new List<LabURI>();
            Positions = new List<LabURI>();
            Paths = new List<LabURI>();
            ObjectId = LabURI.Empty;
            OnSpawnScriptId = LabURI.Empty;
            ParamList1 = new List<UInt32>();
            ParamList2 = new List<float>();
            ParamList3 = new List<UInt32>();
        }

        public ObjectInstanceData(ITwinInstance instance) : this()
        {
            SetTwinItem(instance);
        }

        [JsonProperty(Required = Required.Always)]
        public Vector4 Position { get; set; }
        [JsonProperty(Required = Required.Always)]
        public TwinIntegerRotation RotationX { get; set; }
        [JsonProperty(Required = Required.Always)]
        public TwinIntegerRotation RotationY { get; set; }
        [JsonProperty(Required = Required.Always)]
        public TwinIntegerRotation RotationZ { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt32 InstancesRelated { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> Instances { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt32 PositionsRelated { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> Positions { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt32 PathsRelated { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> Paths { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI ObjectId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Int16 RefListIndex { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI OnSpawnScriptId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt32 StateFlags { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<UInt32> ParamList1 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Single> ParamList2 { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<UInt32> ParamList3 { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            Instances.Clear();
            Positions.Clear();
            Paths.Clear();
            ParamList1.Clear();
            ParamList2.Clear();
            ParamList3.Clear();
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var assetManager = AssetManager.Get();
            ITwinInstance instance = GetTwinItem<ITwinInstance>();
            Position = CloneUtils.Clone(instance.Position);
            RotationX = CloneUtils.Clone(instance.RotationX);
            RotationY = CloneUtils.Clone(instance.RotationY);
            RotationZ = CloneUtils.Clone(instance.RotationZ);
            InstancesRelated = instance.InstancesRelated;
            Instances = new(instance.Instances.Count);
            foreach (var inst in instance.Instances)
            {
                Instances.Add(assetManager.GetUri(package, nameof(ObjectInstance), variant, layoutId, inst));
            }
            PositionsRelated = instance.PositionsRelated;
            Positions = new(instance.Positions.Count);
            foreach (var pos in instance.Positions)
            {
                Positions.Add(assetManager.GetUri(package, nameof(Assets.Instance.Position), variant, layoutId, pos));
            }
            PathsRelated = instance.PathsRelated;
            Paths = new(instance.Paths.Count);
            foreach (var path in instance.Paths)
            {
                Paths.Add(assetManager.GetUri(package, nameof(Path), variant, layoutId, path));
            }
            ObjectId = assetManager.GetUri(package, nameof(GameObject), variant, instance.ObjectId);
            RefListIndex = instance.RefListIndex;
            OnSpawnScriptId = assetManager.GetUri(package, nameof(BehaviourGraph), variant, instance.OnSpawnHeaderScriptID + 1U);
            StateFlags = instance.StateFlags;
            ParamList1 = CloneUtils.CloneList(instance.ParamList1);
            ParamList2 = CloneUtils.CloneList(instance.ParamList2);
            ParamList3 = CloneUtils.CloneList(instance.ParamList3);
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            Position.Write(writer);
            RotationX.Write(writer);
            RotationY.Write(writer);
            RotationZ.Write(writer);

            writer.Write(Instances.Count);
            writer.Write(Instances.Count);
            writer.Write(InstancesRelated);
            foreach (var inst in Instances)
            {
                writer.Write((UInt16)assetManager.GetAsset(inst).ID);
            }

            writer.Write(Positions.Count);
            writer.Write(Positions.Count);
            writer.Write(PositionsRelated);
            foreach (var pos in Positions)
            {
                writer.Write((UInt16)assetManager.GetAsset(pos).ID);
            }

            writer.Write(Paths.Count);
            writer.Write(Paths.Count);
            writer.Write(PathsRelated);
            foreach (var path in Paths)
            {
                writer.Write((UInt16)assetManager.GetAsset(path).ID);
            }

            writer.Write((UInt16)assetManager.GetAsset(ObjectId).ID);

            writer.Write(RefListIndex);

            writer.Write(OnSpawnScriptId == LabURI.Empty ? UInt16.MaxValue : (UInt16)assetManager.GetAsset(OnSpawnScriptId).ID);
            writer.Write((Byte)ParamList1.Count);
            writer.Write((Byte)ParamList2.Count);
            writer.Write((Byte)ParamList3.Count);
            writer.Write((Byte)0);
            writer.Write(StateFlags);

            writer.Write(ParamList1.Count);
            foreach (var flag in ParamList1)
            {
                writer.Write(flag);
            }

            writer.Write(ParamList2.Count);
            foreach (var @float in ParamList2)
            {
                writer.Write(@float);
            }

            writer.Write(ParamList3.Count);
            foreach (var param in ParamList3)
            {
                writer.Write(param);
            }

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateInstance(ms);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var root = section.GetRoot();
            var codeSection = root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
            var objectsSection = codeSection.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
            var behavioursSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);

            if (layoutID is Constants.LEVEL_LAYOUT_6_SECTION)
            {
                return base.ResolveChunkResources(factory, section, id, layoutID);
            }

            assetManager.GetAsset(ObjectId).ResolveChunkResources(factory, objectsSection);
            if (OnSpawnScriptId != LabURI.Empty)
            {
                assetManager.GetAsset(OnSpawnScriptId).ResolveChunkResources(factory, behavioursSection);
            }

            // Positions, paths and instances don't need to be resolved because they are gonna be resolved by themselves anyway

            return base.ResolveChunkResources(factory, section, id);
        }
    }
}
