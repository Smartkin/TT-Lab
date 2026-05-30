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

public class ChunkLinkBoundingBoxBuilderViewModel : Conductor<IScreen>, ISaveableViewModel<TwinChunkLinkBoundingBoxBuilder>, IHaveChildrenEditors
{
    private Int32 type;
    private BoundingBoxBuilderViewModel boundingBoxBuilder;
    private bool isDirty;
    private DirtyTracker dirtyTracker;

    public ChunkLinkBoundingBoxBuilderViewModel()
    {
        dirtyTracker = new DirtyTracker(this);
        boundingBoxBuilder = new BoundingBoxBuilderViewModel(new TwinBoundingBoxBuilder());
        dirtyTracker.AddChild(boundingBoxBuilder);
    }
    
    public ChunkLinkBoundingBoxBuilderViewModel(TwinChunkLinkBoundingBoxBuilder linkBuilder)
    {
        dirtyTracker = new DirtyTracker(this);
        type = linkBuilder.Type;
        boundingBoxBuilder = new BoundingBoxBuilderViewModel(linkBuilder.BondingBoxBuilder);
        dirtyTracker.AddChild(boundingBoxBuilder);
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

    public void Save(TwinChunkLinkBoundingBoxBuilder linkBuilder)
    {
        linkBuilder.Type = Type;
        BoundingBoxBuilder.Save(linkBuilder.BondingBoxBuilder);
        ResetDirty();
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        ActivateItemAsync(boundingBoxBuilder, cancellationToken);

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

    public BoundingBoxBuilderViewModel BoundingBoxBuilder => boundingBoxBuilder;

    public DirtyTracker DirtyTracker => dirtyTracker;
}