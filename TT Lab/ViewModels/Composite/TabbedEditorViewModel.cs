using Caliburn.Micro;
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Composite;

public class TabbedEditorViewModel(LabURI editableResource, Type editorType) : Conductor<IEditorViewModel>
{
    private LabURI _editableResource = editableResource;
    private Type _editorType = editorType;

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
    {
        var editor = (IEditorViewModel)Locator.Current.GetService(_editorType)!;
        editor.EditableResource = EditableResource;
        return ActivateItemAsync(editor, cancellationToken);
    }

    public override Task<Boolean> CanCloseAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        return ActiveItem.CanCloseAsync(cancellationToken);
    }

    public async Task CloseTab()
    {
        if (await ActiveItem.CanCloseAsync())
        {
            await this.DeactivateAsync(true);
        }
    }

    public LabURI EditableResource
    {
        get => _editableResource;
        set => _editableResource = value;
    }
        
    public Bitmap IconPath => new(ManifestResourceLoader.GetPathInExe($"Media/LabIcons/{AssetManager.Get().GetAsset(EditableResource).IconPath}"));

    public Type EditorType
    {
        get => _editorType;
        set => _editorType = value;
    }
}