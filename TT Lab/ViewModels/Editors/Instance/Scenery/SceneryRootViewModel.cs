using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.ViewModels.Editors.Instance.Scenery
{
    public class SceneryRootViewModel : SceneryNodeViewModel
    {
        private UInt32 treeDepth;
        private IList<SceneryBaseData> baseTree;

        public SceneryRootViewModel(SceneryBaseData data, IList<SceneryBaseData> sceneryTree) : base(data)
        {
            var rootData = (SceneryRootData)data;
            treeDepth = rootData.TreeDepth;
            baseTree = sceneryTree;
        }

        public void BuildTree()
        {
            foreach (var sc in SceneryTypes)
            {
                if (sc.Value == ITwinScenery.SceneryType.Node)
                {
                    var node = baseTree[0];
                    baseTree = baseTree.Skip(1).ToList();
                    var newNode = new SceneryNodeViewModel(node);
                    Children.Add(newNode);
                    newNode.BuildTree(ref baseTree);
                }
                else if (sc.Value == ITwinScenery.SceneryType.Leaf)
                {
                    var leaf = baseTree[0];
                    baseTree = baseTree.Skip(1).ToList();
                    Children.Add(new SceneryLeafViewModel(leaf));
                }
            }
            if (baseTree.Count != 0)
            {
                throw new Exception("Houston, we failed to build the scenery tree correctly!");
            }
            ResetDirty();
        }

        [MarkDirty]
        public UInt32 TreeDepth
        {
            get => treeDepth;
            set
            {
                if (treeDepth != value)
                {
                    treeDepth = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
