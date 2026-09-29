using Caliburn.Micro;
using System;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Instance.ChunkLinks;

public class ChunkLinkHullViewModel : Conductor<IScreen>, ISaveableViewModel<TwinChunkLinkHull>, IHaveChildrenEditors
{
    private Int32 type;
    private readonly CollisionHullViewModel hull;
    private bool isDirty;
    private readonly DirtyTracker dirtyTracker;

    public ChunkLinkHullViewModel()
    {
        dirtyTracker = new DirtyTracker(this);
        hull = new CollisionHullViewModel();
        dirtyTracker.AddChild(hull);
    }

    public ChunkLinkHullViewModel(TwinChunkLinkHull linkHull)
    {
        dirtyTracker = new DirtyTracker(this);
        type = linkHull.Type;
        hull = new CollisionHullViewModel(linkHull.Hull);
        dirtyTracker.AddChild(hull);
    }

    public void ResetDirty()
    {
        dirtyTracker.ResetDirty();
        IsDirty = false;
    }

    public bool IsDirty
    {
        get => isDirty;
        set
        {
            if (isDirty != value)
            {
                isDirty = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public void Save(TwinChunkLinkHull linkHull)
    {
        linkHull.Type = Type;
        Hull.Save(linkHull.Hull);
        ResetDirty();
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        ActivateItemAsync(hull, cancellationToken);

        return base.OnInitializeAsync(cancellationToken);
    }

    [MarkDirty]
    public Int32 Type
    {
        get => type;
        set
        {
            if (value != type)
            {
                type = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public CollisionHullViewModel Hull => hull;

    public DirtyTracker DirtyTracker => dirtyTracker;
}
