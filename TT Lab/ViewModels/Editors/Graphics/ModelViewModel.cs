using System;
using System.Linq;
using System.Numerics;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using ImGuiNET;
using Splat;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using Mesh = TT_Lab.Rendering.Objects.Mesh;

namespace TT_Lab.ViewModels.Editors.Graphics;

public class ModelViewModel : ResourceEditorViewModel
{
    private Mesh? _model;

    public ModelViewModel()
    {
        SceneRenderer = Locator.Current.GetService<ViewportViewModel>()!;
        SceneRenderer.SceneInitializer = (renderer, scene) =>
        {
            var mesh = renderer.GetRenderContext().MeshService.GetMesh(EditableResource);
            if (mesh.Model != null)
            {
                scene.AddChild(mesh.Model);
            }

            var modelData = AssetManager.Get().GetAssetData<ModelData>(EditableResource);
            renderer.RenderImgui += () =>
            {
                ImGui.Begin("Model Data");
                ImGui.SetWindowPos(new Vector2(5, 5));
                ImGui.SetWindowSize(new Vector2(150, 90));
                ImGui.Text($"Vertexes {modelData.Vertexes.Sum(v => v.Count)}");
                ImGui.Text($"Faces {modelData.Faces.Sum(f => f.Count)}");
                ImGui.Text($"Meshes {modelData.Meshes.Count}");
                ImGui.End();
            };
        };
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        // await ActivateItemAsync(SceneRenderer, cancellationToken);
            
        await base.OnActivatedAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        // await DeactivateItemAsync(SceneRenderer, close, cancellationToken);
            
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public ViewportViewModel SceneRenderer { get; }

    public override void LoadData()
    {
        return;
    }

    protected override void Save()
    {
        return;
    }
}