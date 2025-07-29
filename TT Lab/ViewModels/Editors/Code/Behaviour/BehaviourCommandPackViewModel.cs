using System.IO;
using Caliburn.Micro;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.ViewModels.Editors.Code.Behaviour;

public class BehaviourCommandPackViewModel : Screen, IHaveParentEditor<GameObjectViewModel>, ISaveableViewModel<string>
{
    private string code;
    private bool _isDirty;
    private DirtyTracker _dirtyTracker;

    public BehaviourCommandPackViewModel(GameObjectViewModel parent, string code)
    {
        ParentEditor = parent;
        this.code = code;
        _dirtyTracker = new DirtyTracker(this);
    }

    [MarkDirty]
    public string Code
    {
        get => code;
        set
        {
            if (code != value)
            {
                code = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    public void Save(string o)
    {
        o = Code[..];
    }

    public void ResetDirty()
    {
        _dirtyTracker.ResetDirty();
    }

    public GameObjectViewModel ParentEditor { get; set; }

    public bool IsDirty => _dirtyTracker.IsDirty;
}