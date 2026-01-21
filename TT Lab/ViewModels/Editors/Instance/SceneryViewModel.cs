using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Instance.Scenery;

namespace TT_Lab.ViewModels.Editors.Instance;

public class SceneryViewModel : InstanceSectionResourceEditorViewModel
{

    private LabURI _chunk = LabURI.Empty;
    private UInt32 _unkUInt;
    private Byte _unkByte;
    private LabURI _skydome = LabURI.Empty;
    private SceneryRootViewModel? _sceneryTree;

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<SceneryData>();
        data.ChunkPath = _chunk;
        data.FogColor = UnkUInt;
        data.UnkByte = UnkByte;
        data.SkydomeID = Skydome;
        data.Sceneries.Clear();
        if (SceneryTree != null)
        {
            var root = new SceneryRootData(SceneryTree);
            data.Sceneries.Add(root);
            IList<SceneryBaseData> list = data.Sceneries;
            SceneryTree.CompileTree(ref list);
            data.Sceneries = (List<SceneryBaseData>)list;
        }
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<SceneryData>();
        _chunk = data.ChunkPath;
        _unkUInt = data.FogColor;
        _unkByte = data.UnkByte;
        _skydome = data.SkydomeID;
        if (data.Sceneries.Count != 0)
        {
            _sceneryTree = new SceneryRootViewModel(data.Sceneries[0], data.Sceneries.Skip(1).ToList());
            _sceneryTree.BuildTree();
            DirtyTracker.AddChild(_sceneryTree);
        }
            
        ResetDirty();
    }

    [MarkDirty]
    public LabURI Chunk
    {
        get => _chunk;
        set
        {
            if (_chunk != value)
            {
                _chunk = value;
                NotifyOfPropertyChange();
            }
        }
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
    public Byte UnkByte
    {
        get => _unkByte;
        set
        {
            if (_unkByte != value)
            {
                _unkByte = value;
                NotifyOfPropertyChange();
            }
        }
    }
    [MarkDirty]
    public LabURI Skydome
    {
        get => _skydome;
        set
        {
            if (value != _skydome)
            {
                _skydome = value;
                NotifyOfPropertyChange();
            }
        }
    }
    public SceneryRootViewModel? SceneryTree
    {
        get => _sceneryTree;
    }
}