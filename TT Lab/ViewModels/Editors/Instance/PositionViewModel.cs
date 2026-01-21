using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors.Instance;

public class PositionViewModel : InstanceSectionResourceEditorViewModel
{
    private Vector4ViewModel position = new();
    private Enums.Layouts layId;

    public PositionViewModel()
    {
        DirtyTracker.AddChild(position);
    }

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        asset.LayoutID = (int)LayoutID;
        var data = asset.GetData<PositionData>();
        data.Coords = new Twinsanity.TwinsanityInterchange.Common.Vector4
        {
            X = Position.X,
            Y = Position.Y,
            Z = Position.Z,
            W = Position.W
        };
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var posData = asset.GetData<PositionData>();
        DirtyTracker.RemoveChild(position);
        position = new Vector4ViewModel(posData.Coords);
        DirtyTracker.AddChild(position);
        layId = MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value);
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        await base.OnInitializeAsync(cancellationToken);

        await ActivateItemAsync(position, cancellationToken);
    }

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

    public Vector4ViewModel Position => position;
}