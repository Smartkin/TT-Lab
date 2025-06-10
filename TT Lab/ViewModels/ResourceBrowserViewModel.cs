using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.Assets;
using TT_Lab.Command;

namespace TT_Lab.ViewModels;

public class ResourceBrowserViewModel : Screen
{
    private BindableCollection<LabURI> _resourcesToBrowse;
    private BindableCollection<LabURI> _resourcesToBrowseView;
    private string _searchAsset = string.Empty;

    public ResourceBrowserViewModel(Type browseType, LabURI? selectedLink = null)
    {
        _resourcesToBrowse = new BindableCollection<LabURI> { LabURI.Empty };
        SelectedLink = selectedLink == null ? _resourcesToBrowse[0] : selectedLink;
        _resourcesToBrowse.AddRange(AssetManager.Get().GetAllAssetsOf(browseType).Select(a => a.URI));
        _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowse.Distinct().Order());
    }
    
    public ResourceBrowserViewModel(IEnumerable<LabURI> resourcesToBrowse, LabURI? selectedLink = null)
    {
        _resourcesToBrowse = new BindableCollection<LabURI>(resourcesToBrowse);
        SelectedLink = selectedLink == null ? _resourcesToBrowse[0] : selectedLink;
        _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowse.Distinct().Order());
    }

    public ResourceBrowserViewModel(Type browseType, IEnumerable<LabURI> resourcesToBrowse, LabURI? selectedLink = null)
    {
        _resourcesToBrowse = new BindableCollection<LabURI>(resourcesToBrowse.Where(link => link == LabURI.Empty || AssetManager.Get().GetAsset(link).GetType().IsAssignableTo(browseType)));
        SelectedLink = selectedLink == null ? _resourcesToBrowse[0] : selectedLink;
        _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowse.Distinct().Order());
    }

    public void Link()
    {
        TryCloseAsync(true);
    }

    public void Filter(ICommand filterCommand)
    {
        filterCommand.Execute(this);
    }

    public void Filter(Func<LabURI, bool> filter)
    {
        _resourcesToBrowse = new BindableCollection<LabURI>(_resourcesToBrowse.Where(filter));
        _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowse.Distinct().Order());
    }

    private void DoSearch()
    {
        if (_searchAsset == string.Empty)
        {
            _resourcesToBrowseView = _resourcesToBrowse;
        }
        else
        {
            _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowse.Where(uri =>
            {
                if (uri == LabURI.Empty)
                {
                    return LabURI.Empty.ToString().Contains(_searchAsset, StringComparison.CurrentCultureIgnoreCase);
                }

                var asset = AssetManager.Get().GetAsset(uri);
                return asset.Name.Contains(_searchAsset, StringComparison.CurrentCultureIgnoreCase)
                       || asset.Alias.Contains(_searchAsset, StringComparison.CurrentCultureIgnoreCase)
                       || asset.Data.Contains(_searchAsset, StringComparison.CurrentCultureIgnoreCase);
            }));
        }

        _resourcesToBrowseView = new BindableCollection<LabURI>(_resourcesToBrowseView.Distinct().Order());
        NotifyOfPropertyChange(nameof(ResourcesToBrowseView));
    }

    public LabURI SelectedLink { get; set; }
    public BindableCollection<LabURI> ResourcesToBrowseView => _resourcesToBrowseView;

    public string SearchAsset
    {
        get => _searchAsset;
        set
        {
            if (value != _searchAsset)
            {
                _searchAsset = value;
                DoSearch();
            }
        }
    }
}