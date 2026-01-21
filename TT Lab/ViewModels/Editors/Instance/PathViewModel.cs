using System.Linq;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.Editors.Instance;

public class PathViewModel : InstanceSectionResourceEditorViewModel
{
    private Enums.Layouts _layoutId;
    private BindableCollection<Vector4ViewModel> _points = new();
    private BindableCollection<Vector2ViewModel> _arguments = new();

    public PathViewModel()
    {
        DirtyTracker.AddBindableCollection(_points);
        DirtyTracker.AddBindableCollection(_arguments);
    }

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        asset.LayoutID = (int)LayoutID;
        var data = asset.GetData<PathData>();
        data.Points.Clear();
        foreach (var p in Points)
        {
            var v = new Twinsanity.TwinsanityInterchange.Common.Vector4();
            p.Save(v);
            data.Points.Add(v);
        }
        data.Parameters.Clear();
        foreach (var p in Arguments)
        {
            var v = new Twinsanity.TwinsanityInterchange.Common.Vector2();
            p.Save(v);
            data.Parameters.Add(v);
        }
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var pathData = asset.GetData<PathData>();
        _points = [];
        foreach (var vm in pathData.Points.Select(p => new Vector4ViewModel(p)))
        {
            _points.Add(vm);
        }
        DirtyTracker.AddBindableCollection(_points);
        
        _arguments = [];
        foreach (var vm in pathData.Parameters.Select(p => new Vector2ViewModel(p)))
        {
            _arguments.Add(vm);
        }
        DirtyTracker.AddBindableCollection(_arguments);
        
        _layoutId = MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value);

        AddArgumentCommand = new AddItemToListCommand<Vector2ViewModel>(Arguments);
        AddPointCommand = new AddItemToListCommand<Vector4ViewModel>(Points);
        DeleteArgumentCommand = new DeleteItemFromListCommand(Arguments);
        DeletePointCommand = new DeleteItemFromListCommand(Points);
    }

    public AddItemToListCommand<Vector2ViewModel> AddArgumentCommand { get; private set; }
    public AddItemToListCommand<Vector4ViewModel> AddPointCommand { get; private set; }
    public DeleteItemFromListCommand DeleteArgumentCommand { get; private set; }
    public DeleteItemFromListCommand DeletePointCommand { get; private set; }

    [MarkDirty]
    public Enums.Layouts LayoutID
    {
        get => _layoutId;
        set
        {
            if (value != _layoutId)
            {
                _layoutId = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }

    public BindableCollection<Vector4ViewModel> Points
    {
        get => _points;
    }

    public BindableCollection<Vector2ViewModel> Arguments
    {
        get => _arguments;
    }
}