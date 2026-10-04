using System;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;

namespace TT_Lab.ViewModels.Editors.Instance;

public class SceneryViewModel : InstanceSectionResourceEditorViewModel
{
    private UInt32 _unkUInt;
    private Byte _unusedByte;

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<SceneryData>();
        data.FogColor = UnkUInt;
        data.UnusedByte = UnusedByte;
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<SceneryData>();
        _unkUInt = data.FogColor;
        _unusedByte = data.UnusedByte;
        ResetDirty();
    }

    [MarkDirty]
    public UInt32 UnkUInt
    {
        get => _unkUInt;
        set
        {
            if (value != _unkUInt)
            {
                _unkUInt = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public Byte UnusedByte
    {
        get => _unusedByte;
        set
        {
            if (_unusedByte != value)
            {
                _unusedByte = value;
                NotifyOfPropertyChange();
            }
        }
    }
}