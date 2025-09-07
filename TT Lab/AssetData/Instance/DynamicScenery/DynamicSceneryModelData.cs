using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;

namespace TT_Lab.AssetData.Instance.DynamicScenery
{
    [ReferencesAssets]
    public class DynamicSceneryModelData
    {
        public Int32 UnkInt { get; set; }
        public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
        public Int32 AnimatedFrames { get; set; }
        public TwinDynamicSceneryAnimation Animation { get; set; }
        public Byte LodFlag { get; set; }
        public LabURI Mesh { get; set; }
        public Vector4[] BoundingBox { get; set; }

        public DynamicSceneryModelData()
        {
            BoundingBoxBuilders = new List<TwinBoundingBoxBuilder>();
            Animation = new TwinDynamicSceneryAnimation();
            Mesh = LabURI.Empty;
            BoundingBox = new[] { new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1) };
        }

        public DynamicSceneryModelData(IAsset owner, TwinDynamicSceneryModel model)
        {
            UnkInt = model.UnkInt;
            BoundingBoxBuilders = CloneUtils.DeepClone(model.BoundingBoxBuilders);
            AnimatedFrames = model.AnimatedFrames;
            Animation = CloneUtils.DeepClone(model.Animation);
            LodFlag = model.LodFlag;
            Mesh = AssetManager.Get().GetUriByTwinId<Mesh>(owner, model.MeshID);
            BoundingBox = new Vector4[2];
            for (Int32 i = 0; i < BoundingBox.Length; i++)
            {
                BoundingBox[i] = CloneUtils.Clone(model.BoundingBox[i]);
            }
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(UnkInt);
            writer.Write(BoundingBoxBuilders.Count);
            foreach (var bbBuilder in BoundingBoxBuilders)
            {
                bbBuilder.Write(writer);
            }
            writer.Write(AnimatedFrames);
            Animation.Write(writer);
            writer.Write(LodFlag);
            writer.Write(AssetManager.Get().GetAsset(Mesh).ID);
            foreach (var v in BoundingBox)
            {
                v.Write(writer);
            }
        }
    }
}
