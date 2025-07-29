using System;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code.Behaviour;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Assets.Code
{
    public sealed class BehaviourGraph : Behaviour
    {
        private int _starterId = -1;

        protected override String DataExt => ".lab";

        public BehaviourGraph() { }

        public BehaviourGraph(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinBehaviourGraph script, TwinBehaviourStarter? starter = null) : base(package, needVariant, variant, id, name)
        {
            assetData = new BehaviourGraphData(script, starter);
            if (starter != null)
            {
                _starterId = (int)starter.GetID();
            }
            RegenerateURI(needVariant);
        }

        public int MapStarterIdToSelf(int starterId)
        {
            if (_starterId == starterId)
            {
                return (int)ID;
            }

            return -1;
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new BehaviourGraphData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }
    }
}
