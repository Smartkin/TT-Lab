using Caliburn.Micro;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite;

public class BoundingBoxViewModel : Conductor<Vector4ViewModel>.Collection.AllActive, ISaveableViewModel<Vector4[]>, IHaveChildrenEditors
{
    private DirtyTracker dirtyTracker;
    private Vector4ViewModel topLeft;
    private Vector4ViewModel bottomRight;

    public BoundingBoxViewModel()
    {
        dirtyTracker = new DirtyTracker(this);
        topLeft = new Vector4ViewModel();
        bottomRight = new Vector4ViewModel();
        dirtyTracker.AddChild(topLeft);
        dirtyTracker.AddChild(bottomRight);
    }

    public BoundingBoxViewModel(Vector4[] corners)
    {
        dirtyTracker = new DirtyTracker(this);
        topLeft = new Vector4ViewModel(corners[0]);
        bottomRight = new Vector4ViewModel(corners[1]);
        dirtyTracker.AddChild(topLeft);
        dirtyTracker.AddChild(bottomRight);
    }
    
    public BoundingBoxViewModel(BoundingBox corners)
    {
        dirtyTracker = new DirtyTracker(this);
        topLeft = new Vector4ViewModel(corners.V1);
        bottomRight = new Vector4ViewModel(corners.V2);
        dirtyTracker.AddChild(topLeft);
        dirtyTracker.AddChild(bottomRight);
    }

    public void Save(Vector4[] o)
    {
        o[0] = new Vector4(topLeft.X, topLeft.Y, topLeft.Z, topLeft.W);
        o[1] = new Vector4(bottomRight.X, bottomRight.Y, bottomRight.Z, bottomRight.W);
    }

    public void ResetDirty()
    {
        DirtyTracker.ResetDirty();
    }
    
    public Vector4ViewModel TopLeft => topLeft;
    public Vector4ViewModel BottomRight => bottomRight;

    public bool IsDirty => DirtyTracker.IsDirty;

    public DirtyTracker DirtyTracker => dirtyTracker;
}