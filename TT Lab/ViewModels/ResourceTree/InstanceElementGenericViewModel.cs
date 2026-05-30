using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels.ResourceTree;

public class InstanceElementGenericViewModel<T> : InstanceElementViewModel where T : SerializableInstance, new()
{
    public InstanceElementGenericViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : base(asset, parent)
    {
    }

    protected override void DuplicateInstance()
    {
        var newInstance = new T
        {
            Chunk = Asset.Chunk,
            LayoutID = Asset.LayoutID,
            Variation = Asset.Chunk + Locator.Current.GetService<ProjectManager>()!.OpenedProject!.BasePackage.ID,
            ID = TwinIdGeneratorServiceProvider.GetGeneratorForChunk<T>(Asset.Chunk, MiscUtils.ConvertEnum<Enums.Layouts>(Asset.LayoutID!)).GenerateTwinId(),
            Package = Asset.Package
        };
        newInstance.InvariantName = Asset.InvariantName + newInstance.ID;
        newInstance.Alias = Asset.Name;
        newInstance.RegenerateLinks();
        var dataToCopy = ((SerializableInstance)Asset).GetData();
        newInstance.SetData((AbstractAssetData)CloneUtils.DeepClone(dataToCopy, dataToCopy.GetType()));

        var parent = Parent!;
        var folder = parent.GetAsset<Folder>();
        folder.AddChild(newInstance);
        AssetManager.Get().AddAsset(newInstance);
        newInstance.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        folder.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
        
        parent.AddNewChild(newInstance.GetResourceTreeElement(parent));
        parent.ClearChildren();
        parent.LoadChildrenBack();
        parent.NotifyOfPropertyChange(nameof(parent.Children));

        Duplicate = newInstance.GetResourceTreeElement();
        
        base.DuplicateInstance();
    }
}