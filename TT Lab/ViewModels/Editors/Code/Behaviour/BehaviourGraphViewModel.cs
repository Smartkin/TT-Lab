using AvaloniaEdit.Document;
using Caliburn.Micro;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors.Code.Behaviour;

public class BehaviourGraphViewModel : ResourceEditorViewModel
{
    public override void LoadData()
    {
        var behaviourGraph = AssetManager.Get().GetAssetData<BehaviourGraphData>(EditableResource);
        Code = new TextDocument(behaviourGraph.Graph);
        Code.TextChanged += (sender, args) =>
        {
            DirtyTracker.MarkDirty();
        };
        
        ResetDirty();
    }

    protected override void Save()
    {
        var assetManager = AssetManager.Get();
        var data = assetManager.GetAssetData<BehaviourGraphData>(EditableResource);
        data.Graph = Code.Text[..];
        
        base.Save();
    }

    [MarkDirty]
    public TextDocument Code { get; private set; }
}