using System;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.Animation;

namespace TT_Lab.ViewModels.Editors.Instance;

public abstract class InstanceSectionResourceEditorViewModel : ResourceEditorViewModel, IHaveParentEditor<ChunkEditorViewModel>
{
    public ChunkEditorViewModel ParentEditor { get; set; }

    public BindableCollection<object> InstanceLayers => ViewModelUtil.Layers;

    protected InstanceSectionResourceEditorViewModel()
    {
        IgnoreUnsavedPopup = true;
    }
}