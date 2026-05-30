using Caliburn.Micro;
using System;
using System.Linq;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors.Instance
{
    public class AiPathViewModel : InstanceSectionResourceEditorViewModel
    {
        private BindableCollection<LabURI> positions = new();
        private Enums.Layouts layId;
        private LabURI pathBegin = LabURI.Empty;
        private LabURI pathEnd = LabURI.Empty;
        private UInt16[] args = new UInt16[3];

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset<AiPath>(EditableResource);
            asset.LayoutID = (int)LayoutID;
            var data = (AiPathData)asset.GetData();
            data.PathBegin = pathBegin;
            data.PathEnd = pathEnd;
            data.Args[0] = Arg1;
            data.Args[1] = Arg2;
            data.Args[2] = Arg3;
            
            base.Save();
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset<AiPath>(EditableResource);
            var pathData = (AiPathData)asset.GetData();
            pathBegin = pathData.PathBegin;
            pathEnd = pathData.PathEnd;
            args = CloneUtils.CloneArray(pathData.Args);
            layId = MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value);

            var tree = ParentEditor.ChunkTree;
            var navPositions = tree.Where(avm => avm is InstanceElementGenericViewModel<AiPosition>).ToList();
            foreach (var p in navPositions)
            {
                positions.Add(p.Asset.URI);
            }
        }

        public BindableCollection<LabURI> Positions => positions;

        [MarkDirty]
        public Enums.Layouts LayoutID
        {
            get => layId;
            set
            {
                if (layId != value)
                {
                    layId = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI PathBegin
        {
            get => pathBegin;
            set
            {
                if (pathBegin != value)
                {
                    pathBegin = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI PathEnd
        {
            get => pathEnd;
            set
            {
                if (pathEnd != value)
                {
                    pathEnd = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 Arg1
        {
            get => args[0];
            set
            {
                if (args[0] != value)
                {
                    args[0] = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 Arg2
        {
            get => args[1];
            set
            {
                if (args[1] != value)
                {
                    args[1] = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 Arg3
        {
            get => args[2];
            set
            {
                if (args[2] != value)
                {
                    args[2] = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
