using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;

namespace TT_Lab.ViewModels.Editors.Graphics;

public class SkydomeViewModel : ResourceEditorViewModel
{
    public SkydomeViewModel(RenderContext context, MeshService meshService)
    {
        SceneRenderer = IoC.Get<ViewportViewModel>();
        SceneRenderer.SceneInitializer = (renderer, scene) =>
        {
            var skydomeData = AssetManager.Get().GetAssetData<SkydomeData>(EditableResource);
            var skydome = new Skydome(context, skydomeData, meshService);
            scene.AddChild(skydome);
        };
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ActivateItemAsync(SceneRenderer, cancellationToken);
        
        await base.OnActivateAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await DeactivateItemAsync(SceneRenderer, close, cancellationToken);
        
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public override void LoadData()
    {
        return;
    }
    
    public ViewportViewModel SceneRenderer { get; }
}