using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Instance.ChunkLinks;
using Action = System.Action;

namespace TT_Lab.ViewModels.Editors.Instance;

public class ChunkLinkViewModel : ViewportEditableInstanceViewModel
{
    private readonly BindableCollection<LinkViewModel> _links = [];
    private LinkViewModel? _selectedLink;

    public ChunkLinkViewModel()
    {
        DirtyTracker.AddBindableCollection(_links);
        
        Links.CollectionChanged += LinksOnCollectionChanged;
        Rotated += OnRotated;
        Translated += OnTranslated;
    }

    private void LinksOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var newLinks = new List<ChunkLink>();
        foreach (var link in Links)
        {
            var newLink = new ChunkLink();
            link.Save(newLink);
            newLinks.Add(newLink);
        }
        ParentEditor.UpdateLinkedChunks(newLinks);
    }

    private void OnTranslated(vec3 translation)
    {
        SelectedLink?.TranslateMatrix(translation);
    }

    private void OnRotated(quat rot)
    {
        SelectedLink?.RotateMatrix(rot);
    }

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ChunkLinksData>();
        data.Links.Clear();
        foreach (var link in Links)
        {
            var newLink = new ChunkLink();
            link.Save(newLink);
            data.Links.Add(newLink);
        }
        
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ChunkLinksData>();
        _links.Clear();
        foreach (var link in data.Links)
        {
            _links.Add(new LinkViewModel(link));
        }
        
        DirtyTracker.ResetDirty();
    }

    public void SelectedLinkChanged(SelectedItemChangedEventArgs e)
    {
        SelectedLink = e.GetSelectedItem<LinkViewModel>();
    }
    
    public AddItemToListCommand<LinkViewModel> AddLinkCommand => new(Links);
    public DeleteItemFromListCommand DeleteLinkCommand => new(Links);

    public override Vector4ViewModel Position => SelectedLink!.Position;
    public override Vector3ViewModel Rotation => SelectedLink!.Rotation;

    public LinkViewModel? SelectedLink
    {
        get => _selectedLink ?? Links[0];
        set
        {
            if (value == null)
            {
                return;
            }
            
            _selectedLink = value;
            ParentEditor.SelectDifferentInstance(AssetManager.Get().GetAsset(EditableResource));
            NotifyOfPropertyChange();
        }
    }
    public BindableCollection<LinkViewModel> Links => _links;
}