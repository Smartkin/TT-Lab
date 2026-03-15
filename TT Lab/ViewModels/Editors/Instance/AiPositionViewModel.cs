using Caliburn.Micro;
using System;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Project.Messages;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors.Instance
{
    public class AiPositionViewModel : InstanceSectionResourceEditorViewModel
    {
        private readonly IEventAggregator _eventAggregator;
        private Vector4ViewModel _position = new();
        private UInt16 _arg;
        private Enums.Layouts _layId;

        public AiPositionViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            DirtyTracker.AddChild(_position);
        }

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            asset.LayoutID = (int)LayoutID;
            var data = asset.GetData<AiPositionData>();
            data.Arg = Argument;
            
            base.Save();
        }

        protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
        {
            DeactivateItemAsync(Position, close, cancellationToken);

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var posData = asset.GetData<AiPositionData>();
            DirtyTracker.RemoveChild(_position);
            DirtyTracker.AddChild(_position);
            // TODO: Receive notifications from vector editor to change the position in the scene/chunk renderer
            ActivateItemAsync(_position);
            _arg = posData.Arg;
            _layId = MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value);
            _eventAggregator.PublishOnUIThreadAsync(new ChangeRenderCameraPositionMessage { NewCameraPosition = posData.Coords.ToGlm().xyz });
        }

        [MarkDirty]
        public Enums.Layouts LayoutID
        {
            get => _layId;
            set
            {
                if (_layId != value)
                {
                    _layId = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 Argument
        {
            get => _arg;
            set
            {
                if (_arg != value)
                {
                    _arg = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        public Vector4ViewModel Position
        {
            get => _position;
        }
    }
}
